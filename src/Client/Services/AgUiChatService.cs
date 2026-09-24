using System.Runtime.CompilerServices;
using System.Text.Json;

using AGUI.Abstractions;
using AGUI.Client;

using AGUIWebChat.Client.Models;

using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AGUIWebChat.Client.Services;

public sealed class AgUiChatService
{
    private readonly AIAgent _remoteAgent;
    private readonly AgentSession _session;
    private readonly TelemetryService _telemetry;
    public event Action<string>? RunStarted;

    public AgUiChatService(HttpClient httpClient, TelemetryService telemetry)
    {
        _telemetry = telemetry;
        var chatClient = new AGUIChatClient(new AGUIChatClientOptions(httpClient, "/ag-ui"));

        _remoteAgent = chatClient.AsAIAgent();

        _session = _remoteAgent
            .CreateSessionAsync()
            .GetAwaiter()
            .GetResult();
    }

    public async IAsyncEnumerable<AgentResponseUpdate> SendAsync(
        string message,
        ChatSettings settings,
        [EnumeratorCancellation]
        CancellationToken cancellationToken = default)
    {
        var state = JsonSerializer.SerializeToElement(
            new
            {
                thinkingEffort = settings.ThinkingEffort.ToString(),
                temperature = settings.Temperature,
                topP = settings.TopP,
                topK = settings.TopK,
                numCtx = settings.NumCtx,
                telemetryChannel = _telemetry.Channel
            });

        var runOptions = new ChatClientAgentRunOptions
        {
            ChatOptions = new ChatOptions
            {
                RawRepresentationFactory = _ =>
                    new RunAgentInput
                    {
                        State = state
                    }
            }
        };

        await foreach (var update in _remoteAgent.RunStreamingAsync(
            message,
            _session,
            runOptions,
            cancellationToken))
        {
            var chatUpdate = update.AsChatResponseUpdate();

            if (chatUpdate.RawRepresentation is RunStartedEvent runStarted)
            {
                RunStarted?.Invoke(runStarted.RunId);
            }

            yield return update;
        }
    }
}
