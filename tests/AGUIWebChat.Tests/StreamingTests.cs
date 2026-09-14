using AGUIWebChat.Middleware;
using System.Text;
using Xunit;

namespace AGUIWebChat.Tests;

public class StreamingTests
{
    private sealed class EventLogger : IAgUiSseEventLogger
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

    [Theory]
    [InlineData("\n\n", "sync")]
    [InlineData("\r\n\r\n", "sync")]
    [InlineData("\n\n", "array")]
    [InlineData("\r\n\r\n", "array")]
    [InlineData("\n\n", "memory")]
    [InlineData("\r\n\r\n", "memory")]
    public async Task ReaderAndWriterDecodeTheSameFragmentedEvents(string separator, string api)
    {
        var input = Encoding.UTF8.GetBytes("data: café 😀" + separator + "data: fin" + separator);
        var writtenEvents = new EventLogger();
        using var output = new MemoryStream();
        await using (var writer = new SseLoggingStream(output, writtenEvents))
        {
            foreach (var value in input)
            {
                byte[] fragment = [value];
                if (api == "sync") writer.Write(fragment, 0, 1);
                else if (api == "array") await writer.WriteAsync(fragment, 0, 1, default);
                else await writer.WriteAsync(fragment.AsMemory());
            }
            await writer.FlushAsync(default);
        }
        // The response body belongs to ASP.NET Core and must remain open.
        Assert.True(output.CanWrite);
        Assert.Equal(input, output.ToArray());
        var readEvents = new EventLogger();
        output.Position = 0;
        await using var reader = new SseReadLoggingStream(output, readEvents);
        var buffer = new byte[1];
        while (await reader.ReadAsync(buffer.AsMemory()) > 0) { }
        Assert.Equal(new[] { "data: café 😀", "data: fin" }, writtenEvents.Events);
        Assert.Equal(writtenEvents.Events, readEvents.Events);
    }
}
