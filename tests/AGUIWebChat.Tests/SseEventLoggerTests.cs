using AGUIWebChat.Middleware;
using Microsoft.Extensions.Logging;
using Xunit;

namespace AGUIWebChat.Tests;

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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BothApplicationsReconstructTextAndReasoningWithTheSharedLogger(bool client)
    {
        var clientLog = new CaptureLogger<AGUIWebChat.Client.Middleware.AgUiClientSseEventLogger>();
        var serverLog = new CaptureLogger<AGUIWebChat.Server.Middleware.AgUiSseEventLogger>();
        IAgUiSseEventLogger logger = client
            ? new AGUIWebChat.Client.Middleware.AgUiClientSseEventLogger(clientLog)
            : new AGUIWebChat.Server.Middleware.AgUiSseEventLogger(serverLog);
        foreach (var kind in new[] { "TEXT", "REASONING" })
        {
            logger.LogEvent($$"""data: {"type":"{{kind}}_MESSAGE_START","messageId":"m","role":"assistant"}""");
            logger.LogEvent($$"""data: {"type":"{{kind}}_MESSAGE_CONTENT","messageId":"m","delta":"Bon"}""");
            logger.LogEvent($$"""data: {"type":"{{kind}}_MESSAGE_CONTENT","messageId":"m","delta":"jour"}""");
            logger.LogEvent($$"""data: {"type":"{{kind}}_MESSAGE_END","messageId":"m"}""");
        }
        logger.LogEvent("data: {invalid}");
        var entries = client ? clientLog.Entries : serverLog.Entries;
        Assert.Contains(entries, entry => entry.Level == LogLevel.Information && entry.Text.Contains("Content=Bonjour"));
        Assert.Contains(entries, entry => entry.Level == LogLevel.Debug && entry.Text.Contains("Content=Bonjour"));
        Assert.Contains(entries, entry => entry.Level == LogLevel.Warning && entry.Text.Contains("Unable to parse"));
    }
}
