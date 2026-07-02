#nullable enable

using System;
using System.IO;
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
            // Use a SecureRandom for the handshake
            var secureRandom = new SecureRandom();

            // Initialize the TLS protocol over the underlying stream
            _protocol = new TlsClientProtocol(underlyingStream, secureRandom);

            var client = new BcTlsClient(host);

            // Attempt to connect (perform handshake)
            _protocol.Connect(client);

            // Obtain the stream for application data
            _tlsStream = _protocol.Stream;
        }

        private sealed class BcTlsClient : DefaultTlsClient
        {
            private readonly string _host;

            public BcTlsClient(string host)
            {
                _host = host;
            }

            public override TlsAuthentication GetAuthentication()
            {
                return new NullTlsAuthentication();
            }
        }

        private sealed class NullTlsAuthentication : TlsAuthentication
        {
            public void NotifyServerCertificate(org.bouncycastle.crypto.tls.Certificate serverCertificate)
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
