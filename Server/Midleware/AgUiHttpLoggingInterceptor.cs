using Microsoft.AspNetCore.HttpLogging;

namespace AGUIWebChatServer.Midleware
{
    public sealed class AgUiHttpLoggingInterceptor : IHttpLoggingInterceptor
    {
        public ValueTask OnRequestAsync(
            HttpLoggingInterceptorContext logContext)
        {
            var path = logContext.HttpContext.Request.Path;

            if (path.StartsWithSegments("/ag-ui"))
            {
                logContext.LoggingFields =
                    HttpLoggingFields.RequestPropertiesAndHeaders |
                    HttpLoggingFields.RequestBody |
                    HttpLoggingFields.ResponsePropertiesAndHeaders |
                    HttpLoggingFields.Duration;
            }
            else
            {
                logContext.LoggingFields =
                    HttpLoggingFields.None;
            }

            return ValueTask.CompletedTask;
        }

        public ValueTask OnResponseAsync(
            HttpLoggingInterceptorContext logContext)
        {
            return ValueTask.CompletedTask;
        }
    }
}
