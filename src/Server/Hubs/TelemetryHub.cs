using Microsoft.AspNetCore.SignalR;

namespace AGUIWebChat.Server.Hubs
{
    public sealed class TelemetryHub : Hub
    {
        public override async Task OnConnectedAsync()
        {
            var channel = Context.GetHttpContext()?.Request.Query["channel"].ToString();
            if (!Guid.TryParseExact(channel, "N", out _))
                throw new HubException("Invalid telemetry channel.");
            await Groups.AddToGroupAsync(Context.ConnectionId, channel!);
            await base.OnConnectedAsync();
        }
    }
}
