using System.Reflection;
using AGUIWebChatServer.Hubs;
using AGUIWebChatServer.Telemetry;
using Microsoft.AspNetCore.SignalR;
using Xunit;

namespace AGUIWebChat.Tests;

public class TelemetryTests
{
    public class Stub : DispatchProxy
    {
        public Func<MethodInfo, object?[], object?> Call { get; set; } = (_, _) => throw new InvalidOperationException();
        protected override object? Invoke(MethodInfo? method, object?[]? args) => Call(method!, args!);
    }

    private static T Proxy<T>(Func<MethodInfo, object?[], object?> callback) where T : class
    {
        var proxy = DispatchProxy.Create<T, Stub>();
        ((Stub)(object)proxy).Call = callback;
        return proxy;
    }

    [Fact]
    public async Task SendsOnlyToRequestedGroup()
    {
        var destinations = new List<string>();
        var client = Proxy<IClientProxy>((method, args) => Task.CompletedTask);
        var clients = Proxy<IHubClients>((method, args) =>
        {
            Assert.Equal("Group", method.Name); // Any broadcast fails this test.
            destinations.Add((string)args[0]!);
            return client;
        });
        var hub = Proxy<IHubContext<TelemetryHub>>((method, args) => clients);
        await TelemetryPublisher.SendAsync(hub, "circuit-a", "Metrics", new { }, default);
        await TelemetryPublisher.SendAsync(hub, "circuit-b", "Metrics", new { }, default);
        Assert.Equal(new[] { "circuit-a", "circuit-b" }, destinations);
    }

    [Fact]
    public async Task MissingChannelAndCancellationDoNotPublish()
    {
        var calls = 0;
        var hub = Proxy<IHubContext<TelemetryHub>>((method, args) => { calls++; throw new IOException(); });
        await TelemetryPublisher.SendAsync(hub, null, "Metrics", new { }, default);
        await TelemetryPublisher.SendAsync(hub, "channel", "Metrics", new { }, new CancellationToken(true));
        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task DeliveryFailureDoesNotEscapeIntoModelStream()
    {
        var hub = Proxy<IHubContext<TelemetryHub>>((method, args) => throw new IOException("Disconnected"));
        await TelemetryPublisher.SendAsync(hub, "channel", "Metrics", new { }, default);
    }
}
