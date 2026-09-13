using System.Text;

namespace AGUIWebChatServer.Middleware
{
    public sealed class SseLoggingStream : Stream
    {
        private readonly Stream _inner;
        private readonly IAgUiSseEventLogger _eventLogger;
        private readonly StringBuilder _buffer = new();

        public SseLoggingStream(Stream inner, IAgUiSseEventLogger eventLogger)
        {
            _inner = inner;
            _eventLogger = eventLogger;
        }

        private void Capture(ReadOnlySpan<byte> buffer)
        {
            string text =
                Encoding.UTF8.GetString(buffer);

            _buffer.Append(text);

            ProcessBuffer();
        }

        private void ProcessBuffer()
        {
            while (true)
            {
                string content = _buffer.ToString();

                int separatorIndex =
                    content.IndexOf(
                        "\n\n",
                        StringComparison.Ordinal);

                if (separatorIndex < 0)
                    return;

                string sseEvent =
                    content[..separatorIndex];

                _buffer.Remove(
                    0,
                    separatorIndex + 2);

                if (!string.IsNullOrWhiteSpace(sseEvent))
                {
                    _eventLogger.LogEvent(sseEvent);
                }
            }
        }

        public override async ValueTask WriteAsync(
            ReadOnlyMemory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            Capture(buffer.Span);

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
            Capture(buffer.AsSpan(offset, count));

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
            Capture(buffer.AsSpan(offset, count));

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
