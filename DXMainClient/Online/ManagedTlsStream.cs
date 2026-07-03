#nullable enable

using System;
using System.Collections;
using System.IO;
using System.Text;
using Org.BouncyCastle.Crypto.Tls;
using Org.BouncyCastle.Security;

namespace DTAClient.Online
{
    // Minimal wrapper to provide a Stream over a BouncyCastle TLS connection.
    // This class attempts to perform a TLS handshake using BouncyCastle's TlsClientProtocol
    // and exposes the resulting secured stream for read/write operations.
    internal sealed class ManagedTlsStream : Stream
    {
        private readonly TlsClientProtocol _protocol;
        private readonly Stream _tlsStream;

        public ManagedTlsStream(Stream underlyingStream, string host)
        {
            var secureRandom = new SecureRandom();
            _protocol = new TlsClientProtocol(underlyingStream, secureRandom);

            var client = new BcTlsClient(host);
            _protocol.Connect(client);
            _tlsStream = _protocol.Stream;
        }

        private sealed class BcTlsClient : DefaultTlsClient
        {
            private readonly string _host;

            public BcTlsClient(string host)
            {
                _host = host;
            }

            public override void NotifyAlertRaised(byte alertLevel, byte alertDescription, string message, Exception? cause)
            {
                // Preserve default behavior.
                base.NotifyAlertRaised(alertLevel, alertDescription, message, cause);
            }

            public override TlsAuthentication GetAuthentication()
            {
                return new NullTlsAuthentication();
            }

            public override IDictionary GetClientExtensions()
            {
                var extensions = base.GetClientExtensions() ?? new Hashtable();

                // Manually build the SNI extension (server_name)
                byte[] hostNameBytes = Encoding.UTF8.GetBytes(_host);
                byte[] extensionData = new byte[hostNameBytes.Length + 5];

                // Server name list length (2 bytes)
                extensionData[0] = (byte)((hostNameBytes.Length + 3) >> 8);
                extensionData[1] = (byte)(hostNameBytes.Length + 3);
                // Name type (1 byte) - 0 = host_name
                extensionData[2] = 0;
                // Name length (2 bytes)
                extensionData[3] = (byte)(hostNameBytes.Length >> 8);
                extensionData[4] = (byte)hostNameBytes.Length;
                // Name data
                Array.Copy(hostNameBytes, 0, extensionData, 5, hostNameBytes.Length);

                // Extension type 0 = server_name
                extensions[0] = extensionData;

                return extensions;
            }

            /// <summary>
            /// Overrides the default cipher suites to offer only modern, secure ciphers
            /// that are compatible with modern TLS servers. The default list includes
            /// obsolete ciphers (RC4, 3DES, NULL) that can cause handshake failures.
            /// </summary>
            public override int[] GetCipherSuites()
            {
                return new int[]
                {
                    CipherSuite.TLS_ECDHE_RSA_WITH_AES_128_GCM_SHA256,
                    CipherSuite.TLS_ECDHE_RSA_WITH_AES_256_GCM_SHA384,
                    CipherSuite.TLS_DHE_RSA_WITH_AES_128_GCM_SHA256,
                    CipherSuite.TLS_DHE_RSA_WITH_AES_256_GCM_SHA384,
                    CipherSuite.TLS_ECDHE_RSA_WITH_AES_128_CBC_SHA256,
                    CipherSuite.TLS_ECDHE_RSA_WITH_AES_256_CBC_SHA384,
                    CipherSuite.TLS_DHE_RSA_WITH_AES_128_CBC_SHA256,
                    CipherSuite.TLS_DHE_RSA_WITH_AES_256_CBC_SHA256,
                    CipherSuite.TLS_ECDHE_RSA_WITH_AES_128_CBC_SHA,
                    CipherSuite.TLS_ECDHE_RSA_WITH_AES_256_CBC_SHA,
                    CipherSuite.TLS_DHE_RSA_WITH_AES_128_CBC_SHA,
                    CipherSuite.TLS_DHE_RSA_WITH_AES_256_CBC_SHA,
                    CipherSuite.TLS_RSA_WITH_AES_128_GCM_SHA256,
                    CipherSuite.TLS_RSA_WITH_AES_256_GCM_SHA384,
                };
            }
        }

        private sealed class NullTlsAuthentication : TlsAuthentication
        {
            public void NotifyServerCertificate(Certificate serverCertificate)
            {
                // Accept any server certificate.
            }

            public TlsCredentials GetClientCredentials(CertificateRequest certificateRequest)
            {
                return null!;
            }
        }

        public override bool CanRead => _tlsStream.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => _tlsStream.CanWrite;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override void Flush() => _tlsStream.Flush();
        public override int Read(byte[] buffer, int offset, int count) => _tlsStream.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => _tlsStream.Write(buffer, offset, count);

        protected override void Dispose(bool disposing)
        {
            try { _tlsStream.Dispose(); } catch { }
            base.Dispose(disposing);
        }
    }
}