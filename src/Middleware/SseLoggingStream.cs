

namespace AGUIWebChat.Middleware
{
    public sealed class SseLoggingStream : Stream
    {
        private readonly Stream _inner;
        private readonly SseEventDecoder _decoder;


        public SseLoggingStream(Stream inner, IAgUiSseEventLogger eventLogger)
        {
            _inner = inner;
            _decoder = new SseEventDecoder(eventLogger);
        }

        public override async ValueTask WriteAsync(
            ReadOnlyMemory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            _decoder.Capture(buffer.Span);

            await _inner.WriteAsync(
                buffer,
                cancellationToken);
        }

        public override async Task WriteAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken)
        {
            _decoder.Capture(buffer.AsSpan(offset, count));

            await _inner.WriteAsync(
                buffer,
                offset,
                count,
                cancellationToken);
        }

        public override void Write(
            byte[] buffer,
            int offset,
            int count)
        {
            _decoder.Capture(buffer.AsSpan(offset, count));

            _inner.Write(buffer, offset, count);
        }

        public override void Flush()
            => _inner.Flush();

        public override Task FlushAsync(
            CancellationToken cancellationToken)
            => _inner.FlushAsync(cancellationToken);

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;

        public override long Length =>
            throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(
            byte[] buffer,
            int offset,
            int count) =>
            throw new NotSupportedException();

        public override long Seek(
            long offset,
            SeekOrigin origin) =>
            throw new NotSupportedException();

        public override void SetLength(long value) =>
            throw new NotSupportedException();
    }
}
