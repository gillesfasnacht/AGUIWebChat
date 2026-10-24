using AGUI.Abstractions;
using AGUI.Client;
using AGUIWebChat.Client.Models;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace AGUIWebChat.Client.Services;

public sealed class AgUiChatService
{
    private readonly AIAgent _remoteAgent;
    private readonly HttpClient _httpClient;
    private readonly ConversationIdentity _identity;
    private readonly TelemetryService _telemetry;
    public event Action<string>? RunStarted;

    public AgUiChatService(HttpClient httpClient, TelemetryService telemetry, ConversationIdentity identity)
    {
        _telemetry = telemetry;
        _httpClient = httpClient;
        _identity = identity;

        var chatClient = new AGUIChatClient(new AGUIChatClientOptions(httpClient, "/ag-ui"));

        _remoteAgent = chatClient.AsAIAgent();

    }

    public async IAsyncEnumerable<AgentResponseUpdate> SendAsync(
        string message,
        Guid conversationId,
        Guid messageId,
        ChatSettings settings,
        [EnumeratorCancellation]
        CancellationToken cancellationToken = default)
    {
        _httpClient.DefaultRequestHeaders.Remove("X-Conversation-Key");
        _httpClient.DefaultRequestHeaders.Add("X-Conversation-Key", await _identity.GetKeyAsync(cancellationToken));
        var session = await _remoteAgent.CreateSessionAsync(cancellationToken);
        var state = JsonSerializer.SerializeToElement(
            new
            {
                conversationId,
                messageId,
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

        await foreach (var update in _remoteAgent.RunStreamingAsync(message, session, runOptions, cancellationToken))
        {
            var chatUpdate = update.AsChatResponseUpdate();

            if (chatUpdate.RawRepresentation is RunErrorEvent error)
                throw new InvalidOperationException(error.Message);

            if (chatUpdate.RawRepresentation is RunStartedEvent runStarted)
            {
                RunStarted?.Invoke(runStarted.RunId);
            }

            yield return update;
        }
    }
}
