namespace AGUIWebChat.Client.Middleware
{
    public sealed class AgUiHttpLoggingHandler : DelegatingHandler
    {
        private readonly ILogger<AgUiHttpLoggingHandler> _logger;
        private readonly IAgUiClientSseEventLogger _eventLogger;

        public AgUiHttpLoggingHandler(
            ILogger<AgUiHttpLoggingHandler> logger,
            IAgUiClientSseEventLogger eventLogger)
        {
            _logger = logger;
            _eventLogger = eventLogger;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation(
                "AG-UI HTTP {Method} {Uri}",
                request.Method,
                request.RequestUri);

            if (request.Content is not null)
            {
                string requestContent =
                    await request.Content
                        .ReadAsStringAsync(
                            cancellationToken);

                _logger.LogDebug(
                    "AG-UI Request Body: {RequestBody}",
                    requestContent);
            }

            HttpResponseMessage response =
                await base.SendAsync(
                    request,
                    cancellationToken);

            string? mediaType =
                response.Content.Headers
                    .ContentType?.MediaType;

            _logger.LogInformation(
                "AG-UI HTTP Response {StatusCode} ContentType={ContentType}",
                (int)response.StatusCode,
                mediaType);

            if (string.Equals(
                    mediaType,
                    "text/event-stream",
                    StringComparison.OrdinalIgnoreCase))
            {
                response.Content =
                    new SseLoggingHttpContent(
                        response.Content,
                        _eventLogger);
            }

            return response;
        }
    }
}
