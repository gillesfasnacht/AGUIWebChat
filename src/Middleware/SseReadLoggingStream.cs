

namespace AGUIWebChat.Middleware
{
    public sealed class SseReadLoggingStream : Stream
    {
        private readonly Stream _inner;
        private readonly SseEventDecoder _decoder;

        public SseReadLoggingStream(
            Stream inner,
            IAgUiSseEventLogger eventLogger)
        {
            _inner = inner;
            _decoder = new SseEventDecoder(eventLogger);
        }

        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            int bytesRead =
                await _inner.ReadAsync(
                    buffer,
                    cancellationToken);

            if (bytesRead > 0)
            {
                _decoder.Capture(buffer.Span[..bytesRead]);
            }
            else
            {
                _decoder.Complete();
            }

            return bytesRead;
        }

        public override async Task<int> ReadAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken)
        {
            int bytesRead =
                await _inner.ReadAsync(
                    buffer,
                    offset,
                    count,
                    cancellationToken);

            if (bytesRead > 0)
            {
                _decoder.Capture(
                    buffer.AsSpan(
                        offset,
                        bytesRead));
            }
            else
            {
                _decoder.Complete();
            }

            return bytesRead;
        }

        public override int Read(
            byte[] buffer,
            int offset,
            int count)
        {
            int bytesRead =
                _inner.Read(
                    buffer,
                    offset,
                    count);

            if (bytesRead > 0)
            {
                _decoder.Capture(
                    buffer.AsSpan(
                        offset,
                        bytesRead));
            }
            else
            {
                _decoder.Complete();
            }

            return bytesRead;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _inner.Dispose();
            }

            base.Dispose(disposing);
        }

        public override async ValueTask DisposeAsync()
        {
            await _inner.DisposeAsync();

            GC.SuppressFinalize(this);
        }

        public override bool CanRead =>
            _inner.CanRead;

        public override bool CanSeek =>
            false;

        public override bool CanWrite =>
            false;

        public override long Length =>
            throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
            => throw new NotSupportedException();

        public override long Seek(
            long offset,
            SeekOrigin origin)
            => throw new NotSupportedException();

        public override void SetLength(
            long value)
            => throw new NotSupportedException();

        public override void Write(
            byte[] buffer,
            int offset,
            int count)
            => throw new NotSupportedException();
    }
}
