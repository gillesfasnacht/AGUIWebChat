using System.Text;

namespace AGUIFluentUIChatClient.Midleware
{
    public sealed class SseReadLoggingStream : Stream
    {
        private readonly Stream _inner;
        private readonly IAgUiClientSseEventLogger _eventLogger;
        private readonly ILogger _logger;

        private readonly Decoder _decoder =
            Encoding.UTF8.GetDecoder();

        private readonly StringBuilder _textBuffer = new();

        private bool _completed;

        public SseReadLoggingStream(
            Stream inner,
            IAgUiClientSseEventLogger eventLogger)
        {
            _inner = inner;
            _eventLogger = eventLogger;
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
                Capture(buffer.Span[..bytesRead]);
            }
            else
            {
                Complete();
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
                Capture(
                    buffer.AsSpan(
                        offset,
                        bytesRead));
            }
            else
            {
                Complete();
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
                Capture(
                    buffer.AsSpan(
                        offset,
                        bytesRead));
            }
            else
            {
                Complete();
            }

            return bytesRead;
        }

        private void Capture(
            ReadOnlySpan<byte> bytes)
        {
            Span<char> chars =
                stackalloc char[
                    Encoding.UTF8.GetMaxCharCount(bytes.Length)];

            _decoder.Convert(
                bytes,
                chars,
                flush: false,
                out _,
                out int charsUsed,
                out _);

            if (charsUsed == 0)
                return;

            _textBuffer.Append(
                chars[..charsUsed]);

            ProcessBuffer();
        }

        private void ProcessBuffer()
        {
            while (true)
            {
                string content =
                    _textBuffer.ToString();

                int separatorIndex =
                    FindEventSeparator(
                        content,
                        out int separatorLength);

                if (separatorIndex < 0)
                    return;

                string sseEvent =
                    content[..separatorIndex];

                _textBuffer.Remove(
                    0,
                    separatorIndex + separatorLength);

                if (!string.IsNullOrWhiteSpace(sseEvent))
                {
                    _eventLogger.LogEvent(sseEvent);
                }
            }
        }

        private static int FindEventSeparator(
            string content,
            out int separatorLength)
        {
            int crlf =
                content.IndexOf(
                    "\r\n\r\n",
                    StringComparison.Ordinal);

            int lf =
                content.IndexOf(
                    "\n\n",
                    StringComparison.Ordinal);

            if (crlf < 0 && lf < 0)
            {
                separatorLength = 0;
                return -1;
            }

            if (crlf >= 0 &&
                (lf < 0 || crlf <= lf))
            {
                separatorLength = 4;
                return crlf;
            }

            separatorLength = 2;
            return lf;
        }

        private void Complete()
        {
            if (_completed)
                return;

            _completed = true;

            Span<char> chars =
                stackalloc char[8];

            _decoder.Convert(
                ReadOnlySpan<byte>.Empty,
                chars,
                flush: true,
                out _,
                out int charsUsed,
                out _);

            if (charsUsed > 0)
            {
                _textBuffer.Append(
                    chars[..charsUsed]);
            }

            ProcessBuffer();

            if (_textBuffer.Length > 0)
            {
                string remaining =
                    _textBuffer.ToString();

                if (!string.IsNullOrWhiteSpace(remaining))
                {
                    _logger.LogDebug(
                        "AG-UI SSE remaining data: {SseEvent}",
                        remaining);
                }

                _textBuffer.Clear();
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
