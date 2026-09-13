using Microsoft.AspNetCore.SignalR.Client;

namespace AGUIFluentUIChatClient.Services
{
    public sealed class TelemetryService : IAsyncDisposable
    {
        private readonly HubConnection _hubConnection;

        public event Action<string>? TestReceived;

        public event Action<ReasoningTelemetryDto>? ReasoningCompleted;

        public event Action<LlmTelemetryDto>? LlmMetricsCompleted;

        public TelemetryService(string telemetryUrl)
        {
            _hubConnection = new HubConnectionBuilder()
                .WithUrl(telemetryUrl)
                .WithAutomaticReconnect()
                .Build();

            _hubConnection.On<ReasoningTelemetryDto>("ReasoningCompleted",
                telemetry =>
                {
                    ReasoningCompleted?.Invoke(telemetry);
                });

            _hubConnection.On<LlmTelemetryDto>("LlmMetricsCompleted",
                telemetry =>
                {
                    LlmMetricsCompleted?.Invoke(telemetry);
                });
        }

        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            if (_hubConnection.State == HubConnectionState.Disconnected)
            {
                await _hubConnection.StartAsync(cancellationToken);
            }
        }

        public async ValueTask DisposeAsync()
        {
            await _hubConnection.DisposeAsync();
        }
    }

    public sealed class TelemetryTestMessage
    {
        public string Message { get; set; } = string.Empty;

        public DateTimeOffset Timestamp { get; set; }
    }
}
