using AGUIWebChat.Middleware;
using System.Net;

namespace AGUIWebChat.Client.Middleware
{
    public sealed class SseLoggingHttpContent : HttpContent
    {
        private readonly HttpContent _inner;
        private readonly IAgUiSseEventLogger _eventLogger;

        public SseLoggingHttpContent(HttpContent inner, IAgUiSseEventLogger eventLogger)
        {
            _inner = inner;
            _eventLogger = eventLogger;

            foreach (var header in inner.Headers)
            {
                Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        protected override async Task<Stream>
            CreateContentReadStreamAsync()
        {
            Stream stream = await _inner.ReadAsStreamAsync();

            return new SseReadLoggingStream(stream, _eventLogger);
        }

        protected override async Task SerializeToStreamAsync(
            Stream stream,
            TransportContext? context)
        {
            await _inner.CopyToAsync(stream);
        }

        protected override bool TryComputeLength(
            out long length)
        {
            length = 0;

            return false;
        }

        protected override void Dispose(
            bool disposing)
        {
            if (disposing)
            {
                _inner.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
