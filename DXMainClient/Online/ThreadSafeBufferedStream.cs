#nullable enable

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace DTAClient.Online
{
    /// <summary>
    /// A thread-safe wrapper around a Stream that allows concurrent read and write
    /// operations without throwing NotSupportedException.
    ///
    /// Unlike System.IO.BufferedStream, this class does NOT attempt to flush the
    /// read buffer when a write is initiated. Instead, reads and writes go directly
    /// to the underlying stream with a lock protecting each individual operation.
    /// This is necessary because BouncyCastle's TLS stream is not seekable, and
    /// our architecture has a dedicated receive loop reading from the stream while
    /// the send queue loop writes to it.
    ///
    /// This class provides NO read buffering (unlike BufferedStream) because the
    /// WebSocket frame parser in TcpWebSocketClient already reads in small chunks
    /// and the overhead of per-byte reads is acceptable for the low message volume
    /// of a game lobby chat protocol. The primary purpose of this wrapper is to
    /// prevent the "Cannot write to a BufferedStream while the read buffer is not
    /// empty" crash.
    /// </summary>
    internal sealed class ThreadSafeBufferedStream : Stream
    {
        private readonly Stream _inner;
        private readonly int _bufferSize;
        private readonly byte[] _readBuffer;
        private int _readBufferOffset;
        private int _readBufferCount;
        private readonly object _readLock = new();
        private readonly object _writeLock = new();

        public ThreadSafeBufferedStream(Stream inner, int bufferSize)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _bufferSize = bufferSize;
            _readBuffer = new byte[bufferSize];
        }

        public override bool CanRead => _inner.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => _inner.CanWrite;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Flush()
        {
            lock (_writeLock)
            {
                _inner.Flush();
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            lock (_readLock)
            {
                // Serve from the read buffer first
                if (_readBufferCount > 0)
                {
                    int bytesToCopy = Math.Min(count, _readBufferCount);
                    Buffer.BlockCopy(_readBuffer, _readBufferOffset, buffer, offset, bytesToCopy);
                    _readBufferOffset += bytesToCopy;
                    _readBufferCount -= bytesToCopy;
                    return bytesToCopy;
                }

                // Read directly from the inner stream
                return _inner.Read(buffer, offset, count);
            }
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            lock (_readLock)
            {
                // Serve from the read buffer first
                if (_readBufferCount > 0)
                {
                    int bytesToCopy = Math.Min(count, _readBufferCount);
                    Buffer.BlockCopy(_readBuffer, _readBufferOffset, buffer, offset, bytesToCopy);
                    _readBufferOffset += bytesToCopy;
                    _readBufferCount -= bytesToCopy;
                    return bytesToCopy;
                }
            }

            // Read directly from the inner stream (outside the lock to allow concurrent writes)
            return await _inner.ReadAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            lock (_writeLock)
            {
                _inner.Write(buffer, offset, count);
            }
        }

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            lock (_writeLock)
            {
                // Synchronous write to avoid concurrent read/write issues
                _inner.Write(buffer, offset, count);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _inner.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}