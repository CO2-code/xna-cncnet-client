#nullable enable

using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Text;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Tls;
using Org.BouncyCastle.Tls.Crypto.Impl.BC;

namespace DTAClient.Online
{
    /// <summary>
    /// Minimal wrapper to provide a Stream over a BouncyCastle TLS connection.
    /// This class attempts to perform a TLS handshake using BouncyCastle's TlsClientProtocol
    /// and exposes the resulting secured stream for read/write operations.
    /// Uses the new BouncyCastle.Tls API (Org.BouncyCastle.Tls) for modern TLS 1.2 support.
    /// </summary>
    internal sealed class ManagedTlsStream : Stream
    {
        private readonly TlsClientProtocol _protocol;
        private readonly Stream _tlsStream;

        public ManagedTlsStream(Stream underlyingStream, string host)
        {
            _protocol = new TlsClientProtocol(underlyingStream);

            var client = new BcTlsClient(host);
            _protocol.Connect(client);
            _tlsStream = _protocol.Stream;
        }

        private sealed class BcTlsClient : DefaultTlsClient
        {
            private readonly string _host;
            private static readonly ProtocolVersion[] ProtocolVersions = { ProtocolVersion.TLSv12 };

            public BcTlsClient(string host)
                : base(new BcTlsCrypto(new SecureRandom()))
            {
                _host = host;
            }

            /// <summary>
            /// Offer only TLS 1.2. Some servers (especially Cloudflare/Replit) may
            /// have issues with BouncyCastle's TLS 1.3 implementation.
            /// </summary>
            public override ProtocolVersion[] GetProtocolVersions()
            {
                return ProtocolVersions;
            }

            /// <summary>
            /// Offer only modern AEAD cipher suites that are compatible with
            /// Cloudflare and Replit edge servers.
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

            public override TlsAuthentication GetAuthentication()
            {
                return new NullTlsAuthentication();
            }

            /// <summary>
            /// Provides the SNI (Server Name Indication) hostname so the server
            /// knows which certificate to present. The base GetClientExtensions()
            /// will automatically encode this into the ClientHello.
            /// </summary>
            protected override IList GetSniServerNames()
            {
                byte[] hostNameBytes = Encoding.UTF8.GetBytes(_host);
                return new[] { new ServerName(NameType.host_name, hostNameBytes) };
            }

            public override void NotifyAlertRaised(short alertLevel, short alertDescription, string message, Exception cause)
            {
                Debug.WriteLine($"TLS alert raised: level={alertLevel} desc={alertDescription} msg={message}");
                base.NotifyAlertRaised(alertLevel, alertDescription, message, cause);
            }

            public override void NotifyAlertReceived(short alertLevel, short alertDescription)
            {
                Debug.WriteLine($"TLS alert received: level={alertLevel} desc={alertDescription}");
                base.NotifyAlertReceived(alertLevel, alertDescription);
            }

            public override void NotifyHandshakeComplete()
            {
                Debug.WriteLine("TLS handshake complete");
                base.NotifyHandshakeComplete();
            }

            protected override bool AllowUnexpectedServerExtension(int extensionType, byte[] extensionData)
            {
                // Allow unexpected extensions - some servers (Cloudflare) send extensions
                // that BouncyCastle doesn't expect during handshake
                return true;
            }
        }

        private sealed class NullTlsAuthentication : TlsAuthentication
        {
            public void NotifyServerCertificate(TlsServerCertificate serverCertificate)
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