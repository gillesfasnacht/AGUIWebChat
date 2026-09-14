using System.Text;
namespace AGUIWebChat.Middleware;

internal sealed class SseEventDecoder(IAgUiSseEventLogger eventLogger)
{
    private readonly IAgUiSseEventLogger _eventLogger = eventLogger;
    private readonly Decoder _decoder = Encoding.UTF8.GetDecoder();
    private readonly StringBuilder _textBuffer = new();
    private bool _completed;
        public void Capture(
            ReadOnlySpan<byte> bytes)
        {
            Span<char> chars =
                new char[
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

        public void Complete()
        {
            if (_completed)
                return;

            _completed = true;

            Span<char> chars =
                new char[8];

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
                    _eventLogger.LogEvent(remaining);
                }

                _textBuffer.Clear();
            }
        }

}
