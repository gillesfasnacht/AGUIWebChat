using Serilog;

namespace AGUIWebChatServer.Inference
{
    public sealed class OllamaHttpLoggingHandler : DelegatingHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, 
            CancellationToken cancellationToken)
        {
            Log.Debug("OLLAMA Request {Method} {Uri}", request.Method, request.RequestUri);

            if (request.Content is not null)
            {
                string body = await request.Content.ReadAsStringAsync(
                    cancellationToken);

                Log.Debug("OLLAMA RequestBody: {RequestBody}", body);
            }

            return await base.SendAsync(request, cancellationToken);
        }
    }
}
