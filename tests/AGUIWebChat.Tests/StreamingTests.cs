using System.Text;
using AGUIWebChat.Client.Middleware;
using Xunit;

namespace AGUIWebChat.Tests;

public class StreamingTests
{
    private sealed class EventLogger : IAgUiClientSseEventLogger
    {
        public List<string> Events { get; } = [];
        public void LogEvent(string sseEvent) => Events.Add(sseEvent);
    }

    [Theory]
    [InlineData("data: café\n\ndata: fin\n\n", 2)]
    [InlineData("data: café\r\n\r\ndata: fin\r\n\r\n", 2)]
    [InlineData("data: incomplete", 1)]
    public async Task FragmentedStreamPreservesBytesAndLogsWithoutCrashing(string input, int eventCount)
    {
        var logger = new EventLogger();
        await using var stream = new SseReadLoggingStream(new MemoryStream(Encoding.UTF8.GetBytes(input)), logger);
        using var output = new MemoryStream();
        var buffer = new byte[1];
        while (await stream.ReadAsync(buffer.AsMemory()) > 0)
            output.WriteByte(buffer[0]);
        Assert.Equal(input, Encoding.UTF8.GetString(output.ToArray()));
        Assert.Equal(eventCount, logger.Events.Count);
        Assert.DoesNotContain(logger.Events, item => item.Contains('\uFFFD'));
    }

    [Fact]
    public async Task CancellationReachesUnderlyingStream()
    {
        await using var stream = new SseReadLoggingStream(new MemoryStream([1]), new EventLogger());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => { _ = await stream.ReadAsync(new byte[1].AsMemory(), cancellation.Token); });
    }

    [Fact]
    public async Task DisconnectionErrorIsPreserved()
    {
        await using var stream = new SseReadLoggingStream(new BrokenStream(), new EventLogger());
        await Assert.ThrowsAsync<IOException>(async () => { _ = await stream.ReadAsync(new byte[1].AsMemory()); });
    }

    private sealed class BrokenStream : MemoryStream
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            => ValueTask.FromException<int>(new IOException("Disconnected"));
    }
}
