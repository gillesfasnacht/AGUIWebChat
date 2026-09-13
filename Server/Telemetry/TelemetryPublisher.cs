using AGUIWebChatServer.Hubs;
using Microsoft.AspNetCore.SignalR;
using Serilog;

namespace AGUIWebChatServer.Telemetry;

public static class TelemetryPublisher
{
    public static async Task SendAsync(IHubContext<TelemetryHub> hub, string? channel,
        string method, object telemetry, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(channel) || cancellationToken.IsCancellationRequested)
            return;

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(2));
            await hub.Clients.Group(channel).SendAsync(method, telemetry, timeout.Token);
        }
        catch (Exception ex)
        {
            // Monitoring failures must not replace a model error or interrupt its response.
            Log.Warning(ex, "Telemetry delivery failed for {Method}", method);
        }
    }
}
