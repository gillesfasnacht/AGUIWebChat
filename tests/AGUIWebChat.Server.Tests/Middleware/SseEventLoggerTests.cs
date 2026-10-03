using AGUIWebChat.Middleware;
using Microsoft.Extensions.Logging;
using Xunit;

namespace AGUIWebChat.Server.Tests.Middleware;

public class SseEventLoggerTests
{
    private sealed class CaptureLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Text)> Entries { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
            => Entries.Add((logLevel, formatter(state, exception)));
    }

    [Fact]
    public void LoggerReconstructsTextAndReasoningWithTheSharedLogger()
    {
        var capture = new CaptureLogger<AGUIWebChat.Server.Middleware.AgUiSseEventLogger>();
        IAgUiSseEventLogger logger = new AGUIWebChat.Server.Middleware.AgUiSseEventLogger(capture);
        foreach (var kind in new[] { "TEXT", "REASONING" })
        {
            logger.LogEvent($$"""data: {"type":"{{kind}}_MESSAGE_START","messageId":"m","role":"assistant"}""");
            logger.LogEvent($$"""data: {"type":"{{kind}}_MESSAGE_CONTENT","messageId":"m","delta":"Bon"}""");
            logger.LogEvent($$"""data: {"type":"{{kind}}_MESSAGE_CONTENT","messageId":"m","delta":"jour"}""");
            logger.LogEvent($$"""data: {"type":"{{kind}}_MESSAGE_END","messageId":"m"}""");
        }
        logger.LogEvent("data: {invalid}");
        var entries = capture.Entries;
        Assert.Contains(entries, entry => entry.Level == LogLevel.Information && entry.Text.Contains("Content=Bonjour"));
        Assert.Contains(entries, entry => entry.Level == LogLevel.Debug && entry.Text.Contains("Content=Bonjour"));
        Assert.Contains(entries, entry => entry.Level == LogLevel.Warning && entry.Text.Contains("Unable to parse"));
    }
}
