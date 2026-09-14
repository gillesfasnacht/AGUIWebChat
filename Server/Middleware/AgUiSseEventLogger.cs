namespace AGUIWebChat.Server.Middleware;

// Preserve the server logging category and its configured level overrides.
public sealed class AgUiSseEventLogger(ILogger<AgUiSseEventLogger> logger)
    : AGUIWebChat.Middleware.AgUiSseEventLogger(logger);
