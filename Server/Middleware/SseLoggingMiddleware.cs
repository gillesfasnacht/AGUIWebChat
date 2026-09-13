namespace AGUIWebChatServer.Middleware
{
    public sealed class SseLoggingMiddleware
    {
        private readonly RequestDelegate _next;

        public SseLoggingMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, IAgUiSseEventLogger eventLogger)
        {
            if (!context.Request.Path.StartsWithSegments("/ag-ui"))
            {
                await _next(context);
                return;
            }

            Stream originalBody = context.Response.Body;

            await using var loggingStream = new SseLoggingStream(originalBody, eventLogger);

            context.Response.Body = loggingStream;

            try
            {
                await _next(context);
            }
            finally
            {
                context.Response.Body = originalBody;
            }
        }
    }
}
