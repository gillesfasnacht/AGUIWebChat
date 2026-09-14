namespace AGUIWebChat.Client.Middleware;

// Preserve the client logging category and its configured level overrides.
public sealed class AgUiClientSseEventLogger(ILogger<AgUiClientSseEventLogger> logger)
    : AGUIWebChat.Middleware.AgUiSseEventLogger(logger);
