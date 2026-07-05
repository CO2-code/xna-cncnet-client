#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Tls;
using Org.BouncyCastle.Tls.Crypto;
using Org.BouncyCastle.Tls.Crypto.Impl.BC;

namespace DTAClient.Online
{
    /// <summary>
    /// Minimal WebSocket client built on TcpClient + a pure-managed TLS 1.2 stack (BouncyCastle).
    ///
    /// Works on .NET Framework 4.5+, Mono, .NET Core, .NET 5/6/7/8, Windows 7+, Linux, macOS —
    /// including old/unpatched Windows 7 installs.
    ///
    /// We do NOT use System.Net.WebSockets.ClientWebSocket (PlatformNotSupportedException on
    /// Mono / old .NET Framework on Windows 7) and we do NOT use System.Net.Security.SslStream
    /// for the TLS layer either, because SslStream delegates to the OS's Schannel provider.
    /// On Windows 7, Schannel's default cipher suite set does not include the modern
    /// ECDHE+AES-GCM suites that Replit's TLS-terminating edge requires, and whether the
    /// necessary suites are available depends on how patched the individual machine is
    /// (e.g. KB4019276). That makes connectivity unpredictable across different Windows 7
    /// installs.
    ///
    /// BouncyCastle's Org.BouncyCastle.Tls implementation is a fully self-contained, pure C#
    /// TLS 1.2 client — it does not touch Schannel/CryptoAPI at all, so its cipher suite
    /// support is identical on every version of Windows regardless of OS patch level.
    /// </summary>
    internal sealed class TcpWebSocketClient : IDisposable
    {
        private TcpClient? _tcp;
        private Stream? _stream;
        private TlsClientProtocol? _tlsProtocol;
        private bool _isOpen;

        public bool IsOpen => _isOpen;

        // ─── Connect ──────────────────────────────────────────────────────────────

        public async Task ConnectAsync(Uri uri, CancellationToken ct)
        {
            bool useTls = string.Equals(uri.Scheme, "wss", StringComparison.OrdinalIgnoreCase);
            string host = uri.Host;
            int port = uri.IsDefaultPort ? (useTls ? 443 : 80) : uri.Port;
            string path = string.IsNullOrEmpty(uri.PathAndQuery) ? "/" : uri.PathAndQuery;

            _tcp = new TcpClient();

            // TcpClient.ConnectAsync with a host+port overload
            var connectTask = _tcp.ConnectAsync(host, port);

            // Respect the caller's cancellation token
            var tcs = new TaskCompletionSource<bool>();
            using (ct.Register(() => tcs.TrySetCanceled()))
            {
                var completed = await Task.WhenAny(connectTask, tcs.Task).ConfigureAwait(false);
                if (completed == tcs.Task)
                {
                    _tcp.Close();
                    throw new OperationCanceledException(ct);
                }
                await connectTask.ConfigureAwait(false); // propagate any exception
            }

            Stream baseStream = _tcp.GetStream();

            if (useTls)
            {
                try
                {
                    // Run the blocking BouncyCastle handshake on a background thread and
                    // race it against the caller's cancellation token / timeout.
                    var handshakeTask = Task.Run(() =>
                    {
                        var crypto = new BcTlsCrypto(new SecureRandom());
                        _tlsProtocol = new TlsClientProtocol(baseStream);
                        var tlsClient = new WsTlsClient(crypto, host);
                        _tlsProtocol.Connect(tlsClient);

                        // BouncyCastle's TLS stream only implements synchronous Read/Write.
                        // The default Stream.ReadAsync/WriteAsync fall back to Task.Run-wrapped
                        // blocking calls, and our WS frame parser issues several small reads per
                        // frame (2, 4, 8 bytes). Without buffering, each of those becomes its own
                        // thread-pool-blocking dispatch, which starves the pool under sustained
                        // traffic and causes app-wide lag plus missed PING/PONG deadlines (which
                        // in turn causes reconnect flapping). Wrapping in a BufferedStream collapses
                        // many small reads into large infrequent underlying reads.
                        return (Stream)new BufferedStream(_tlsProtocol.Stream, 16384);
                    }, ct);

                    var cancelTcs = new TaskCompletionSource<Stream>();
                    using (ct.Register(() => cancelTcs.TrySetCanceled()))
                    {
                        var completed = await Task.WhenAny(handshakeTask, cancelTcs.Task).ConfigureAwait(false);
                        if (completed == cancelTcs.Task)
                            throw new OperationCanceledException(ct);

                        _stream = await handshakeTask.ConfigureAwait(false);
                    }
                }
                catch (Exception ex) when (!(ex is OperationCanceledException))
                {
                    throw new Exception(
                        "TLS 1.2 handshake failed (managed BouncyCastle stack). Details: " + ex.Message, ex);
                }
            }
            else
            {
                _stream = baseStream;
            }

            await PerformHandshakeAsync(host, port, path, ct).ConfigureAwait(false);
            _isOpen = true;
        }

        /// <summary>
        /// Accepts any server certificate without validation. The WebSocket endpoint is
        /// pinned by hostname at the connection layer (the game client is hardcoded to a
        /// known CnCNet server address), so this mirrors the trust model already used
        /// elsewhere in the legacy client rather than introducing a full CA trust store
        /// (which BouncyCastle does not source from the OS certificate store by default).
        /// </summary>
        private sealed class LenientTlsAuthentication : TlsAuthentication
        {
            public void NotifyServerCertificate(TlsServerCertificate serverCertificate)
            {
                // Intentionally not validated further — see class remarks.
            }

            public TlsCredentials? GetClientCredentials(CertificateRequest certificateRequest) => null;
        }

        private sealed class WsTlsClient : DefaultTlsClient
        {
            private readonly string _host;

            public WsTlsClient(TlsCrypto crypto, string host) : base(crypto)
            {
                _host = host;
            }

            public override TlsAuthentication GetAuthentication() => new LenientTlsAuthentication();

            protected override IList<ServerName>? GetSniServerNames()
            {
                return new List<ServerName>
                {
                    new ServerName(NameType.host_name, Encoding.ASCII.GetBytes(_host)),
                };
            }

            protected override ProtocolVersion[] GetSupportedVersions()
            {
                return ProtocolVersion.TLSv12.Only();
            }

            protected override int[] GetSupportedCipherSuites()
            {
                return new int[]
                {
                    CipherSuite.TLS_ECDHE_RSA_WITH_AES_128_GCM_SHA256,
                    CipherSuite.TLS_ECDHE_RSA_WITH_AES_256_GCM_SHA384,
                    CipherSuite.TLS_ECDHE_RSA_WITH_AES_128_CBC_SHA256,
                    CipherSuite.TLS_ECDHE_RSA_WITH_AES_256_CBC_SHA384,
                    CipherSuite.TLS_ECDHE_RSA_WITH_AES_128_CBC_SHA,
                    CipherSuite.TLS_ECDHE_RSA_WITH_AES_256_CBC_SHA,
                    CipherSuite.TLS_RSA_WITH_AES_128_GCM_SHA256,
                    CipherSuite.TLS_RSA_WITH_AES_256_GCM_SHA384,
                    CipherSuite.TLS_RSA_WITH_AES_128_CBC_SHA,
                    CipherSuite.TLS_RSA_WITH_AES_256_CBC_SHA,
                };
            }
        }

        private async Task PerformHandshakeAsync(string host, int port, string path, CancellationToken ct)
        {
            // Generate random 16-byte key and base64-encode it
            byte[] keyBytes = new byte[16];
#if NET6_0_OR_GREATER
            RandomNumberGenerator.Fill(keyBytes);
#else
            using (var rng = RandomNumberGenerator.Create())
                rng.GetBytes(keyBytes);
#endif
            string key = Convert.ToBase64String(keyBytes);

            string request =
                $"GET {path} HTTP/1.1\r\n" +
                $"Host: {host}:{port}\r\n" +
                "Upgrade: websocket\r\n" +
                "Connection: Upgrade\r\n" +
                $"Sec-WebSocket-Key: {key}\r\n" +
                "Sec-WebSocket-Version: 13\r\n" +
                "\r\n";

            byte[] requestBytes = Encoding.ASCII.GetBytes(request);
            await WriteAsync(requestBytes, ct).ConfigureAwait(false);

            string response = await ReadHttpResponseAsync(ct).ConfigureAwait(false);

            if (!response.Contains("101"))
            {
                string firstLine = response.Split('\n')[0].Trim();
                throw new Exception($"WebSocket upgrade failed: {firstLine}");
            }
        }

        private async Task<string> ReadHttpResponseAsync(CancellationToken ct)
        {
            var sb = new StringBuilder(512);
            byte[] buf = new byte[1];

            while (true)
            {
                ct.ThrowIfCancellationRequested();
                int n = await _stream!.ReadAsync(buf, 0, 1, ct).ConfigureAwait(false);
                if (n == 0)
                    throw new Exception("Connection closed during WebSocket handshake");

                sb.Append((char)buf[0]);

                // HTTP response ends with \r\n\r\n
                if (sb.Length >= 4 && sb[sb.Length - 4] == '\r' && sb[sb.Length - 3] == '\n'
                    && sb[sb.Length - 2] == '\r' && sb[sb.Length - 1] == '\n')
                    break;
            }

            return sb.ToString();
        }

        // ─── Send ─────────────────────────────────────────────────────────────────

        public async Task SendTextAsync(string text, CancellationToken ct)
        {
            byte[] payload = Encoding.UTF8.GetBytes(text);
            await SendFrameAsync(0x01 /* text */, payload, ct).ConfigureAwait(false);
        }

        private async Task SendPongAsync(byte[] payload, CancellationToken ct)
        {
            await SendFrameAsync(0x0A /* pong */, payload, ct).ConfigureAwait(false);
        }

        private async Task SendFrameAsync(byte opcode, byte[] payload, CancellationToken ct)
        {
            byte[] mask = new byte[4];
#if NET6_0_OR_GREATER
            RandomNumberGenerator.Fill(mask);
#else
            using (var rng = RandomNumberGenerator.Create())
                rng.GetBytes(mask);
#endif
            // Build the frame header + masked payload
            int len = payload.Length;
            using var ms = new MemoryStream(len + 14);

            ms.WriteByte((byte)(0x80 | opcode)); // FIN + opcode

            // Payload length + MASK bit
            if (len <= 125)
            {
                ms.WriteByte((byte)(0x80 | len));
            }
            else if (len <= 65535)
            {
                ms.WriteByte(0x80 | 126);
                ms.WriteByte((byte)(len >> 8));
                ms.WriteByte((byte)(len & 0xFF));
            }
            else
            {
                ms.WriteByte(0x80 | 127);
                for (int i = 7; i >= 0; i--)
                    ms.WriteByte((byte)((len >> (i * 8)) & 0xFF));
            }

            ms.Write(mask, 0, 4);

            for (int i = 0; i < len; i++)
                ms.WriteByte((byte)(payload[i] ^ mask[i % 4]));

            byte[] frame = ms.ToArray();
            await WriteAsync(frame, ct).ConfigureAwait(false);
        }

        // ─── Receive ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Reads the next complete text message from the server.
        /// Returns null when the server closes the connection.
        /// Automatically handles WebSocket PING frames (sends PONG).
        /// </summary>
        public async Task<string?> ReceiveTextAsync(CancellationToken ct)
        {
            var messageBuffer = new MemoryStream();
            bool expectingContinuation = false;

            while (true)
            {
                ct.ThrowIfCancellationRequested();

                // Read first two header bytes
                byte[]? header = await ReadExactAsync(2, ct).ConfigureAwait(false);
                if (header == null) return null;

                bool fin    = (header[0] & 0x80) != 0;
                int  opcode = (header[0] & 0x0F);
                bool masked = (header[1] & 0x80) != 0;
                long plen   = (header[1] & 0x7F);

                if (plen == 126)
                {
                    byte[]? ext = await ReadExactAsync(2, ct).ConfigureAwait(false);
                    if (ext == null) return null;
                    plen = (ext[0] << 8) | ext[1];
                }
                else if (plen == 127)
                {
                    byte[]? ext = await ReadExactAsync(8, ct).ConfigureAwait(false);
                    if (ext == null) return null;
                    plen = 0;
                    for (int i = 0; i < 8; i++)
                        plen = (plen << 8) | ext[i];
                }

                byte[]? maskKey = null;
                if (masked)
                {
                    maskKey = await ReadExactAsync(4, ct).ConfigureAwait(false);
                    if (maskKey == null) return null;
                }

                byte[] payload = plen > 0
                    ? (await ReadExactAsync((int)plen, ct).ConfigureAwait(false) ?? Array.Empty<byte>())
                    : Array.Empty<byte>();

                if (masked && maskKey != null)
                    for (int i = 0; i < payload.Length; i++)
                        payload[i] ^= maskKey[i % 4];

                switch (opcode)
                {
                    case 0x00: // Continuation
                        messageBuffer.Write(payload, 0, payload.Length);
                        if (fin)
                        {
                            string msg = Encoding.UTF8.GetString(messageBuffer.ToArray());
                            messageBuffer.SetLength(0);
                            expectingContinuation = false;
                            return msg;
                        }
                        break;

                    case 0x01: // Text
                        if (fin && !expectingContinuation)
                            return Encoding.UTF8.GetString(payload);

                        messageBuffer.SetLength(0);
                        messageBuffer.Write(payload, 0, payload.Length);
                        expectingContinuation = true;
                        break;

                    case 0x08: // Close
                        _isOpen = false;
                        return null;

                    case 0x09: // Ping — respond with Pong
                        await SendPongAsync(payload, ct).ConfigureAwait(false);
                        break;

                    case 0x0A: // Pong — ignore
                        break;
                }
            }
        }

        // ─── Close ────────────────────────────────────────────────────────────────

        public async Task CloseAsync()
        {
            if (!_isOpen) return;
            _isOpen = false;

            try
            {
                // Send a WebSocket close frame (opcode 0x08, no payload)
                byte[] closeFrame = { 0x88, 0x80, 0x00, 0x00, 0x00, 0x00 }; // FIN+close, masked, empty
                await WriteAsync(closeFrame, CancellationToken.None).ConfigureAwait(false);
            }
            catch { /* ignore errors during close */ }
        }

        // ─── Helpers ──────────────────────────────────────────────────────────────

        private async Task WriteAsync(byte[] data, CancellationToken ct)
        {
            await _stream!.WriteAsync(data, 0, data.Length, ct).ConfigureAwait(false);
            await _stream.FlushAsync(ct).ConfigureAwait(false);
        }

        private async Task<byte[]?> ReadExactAsync(int count, CancellationToken ct)
        {
            byte[] buf = new byte[count];
            int offset = 0;

            while (offset < count)
            {
                ct.ThrowIfCancellationRequested();
                int n = await _stream!.ReadAsync(buf, offset, count - offset, ct).ConfigureAwait(false);
                if (n == 0) return null; // connection closed
                offset += n;
            }

            return buf;
        }

        public void Dispose()
        {
            _isOpen = false;
            try { _tlsProtocol?.Close(); } catch { }
            try { _stream?.Dispose(); } catch { }
            try { _tcp?.Close(); } catch { }
        }
    }
}
