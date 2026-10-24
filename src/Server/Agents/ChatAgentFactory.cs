using AGUIWebChat.Server.Services.Chat;
using AGUI.Abstractions;
using AGUI.Server;
using AGUIWebChat.Server.Telemetry;
using AGUIWebChat.Server.Hubs;
using AGUIWebChat.Server.Inference;
using Microsoft.Agents.AI;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.AI;
using Serilog;
using System.Text.Json;


namespace AGUIWebChat.Server.Agents;

internal static class ChatAgentFactory
{
    public static AIAgent CreateAgent(
        IChatClient chatClient,
        string name,
        string instruction,
        string source,
        string modelName,
        InferenceSettings defaultSettings,
        IHubContext<TelemetryHub> telemetryHub,
        ConversationRunCoordinator conversationRuns,
        IHttpContextAccessor httpContextAccessor)
    {
        Log.Debug("SERVER AGENT_CREATION for {AgentName} with ActivitySource {ActivitySource}", name, source);

        return chatClient
            .AsAIAgent(
                new ChatClientAgentOptions
                {
                    Name = name,
                    ChatOptions = new ChatOptions
                    {
                        Instructions = instruction,

                        AdditionalProperties = new()
                        {
                            ["think"] = defaultSettings.ThinkingEffort,
                            ["temperature"] = defaultSettings.Temperature,
                            ["top_p"] = defaultSettings.TopP,
                            ["top_k"] = defaultSettings.TopK,
                            ["num_ctx"] = defaultSettings.NumCtx
                        }
                    }
                })
            .AsBuilder()
            .UseOpenTelemetry(sourceName: source)
            .Use(runFunc: null,
                runStreamingFunc:
                    (messages, session, options, innerAgent, cancellationToken) =>
                    {
                        var effectiveSettings =
                            InferenceSettingsParser.GetInferenceSettings(
                                options,
                                defaultSettings);

                        var effectiveOptions =
                            InferenceSettingsParser.ApplyInferenceSettings(
                                options,
                                effectiveSettings);

                        Log.Information("SERVER INFERENCE_SETTINGS Agent={AgentName} Model={ModelName} Effort={ThinkingEffort} Temperature={Temperature} TopP={TopP} TopK={TopK} NumCtx={NumCtx}",
                            name,
                            modelName,
                            effectiveSettings.ThinkingEffort,
                            effectiveSettings.Temperature,
                            effectiveSettings.TopP,
                            effectiveSettings.TopK,
                            effectiveSettings.NumCtx);

                        string runId = string.Empty;
                        string? telemetryChannel = null;

                        if (effectiveOptions is ChatClientAgentRunOptions { ChatOptions: { } chatOptions } &&
                            chatOptions.TryGetRunAgentInput(out RunAgentInput? input))
                        {
                            runId = input.RunId;
                            if (input.State is { ValueKind: JsonValueKind.Object } state && state.TryGetProperty("telemetryChannel", out var channel) && channel.ValueKind == JsonValueKind.String && Guid.TryParseExact(channel.GetString(), "N", out _)) telemetryChannel = channel.GetString();
                        }

                        IAsyncEnumerable<AgentResponseUpdate> Generate(
                            IReadOnlyList<ChatMessage> history, CancellationToken token)
                        {
                            var reasoningStream = ReasoningTelemetry.ObserveAsync(
                                history, null, effectiveOptions, innerAgent, telemetryHub,
                                telemetryChannel, runId, name, modelName, effectiveSettings.ThinkingEffort, token);
                            return LlmTelemetry.ObserveAsync(reasoningStream, telemetryHub,
                                telemetryChannel, runId, name, modelName, token);
                        }

                        if (effectiveOptions is not ChatClientAgentRunOptions { ChatOptions: { } persistentOptions } ||
                            !persistentOptions.TryGetRunAgentInput(out RunAgentInput? persistentInput) ||
                            persistentInput.State is not { ValueKind: JsonValueKind.Object } persistentState ||
                            !persistentState.TryGetProperty("conversationId", out var conversationValue) ||
                            conversationValue.ValueKind != JsonValueKind.String ||
                            !conversationValue.TryGetGuid(out var conversationId) ||
                            !persistentState.TryGetProperty("messageId", out var messageValue) ||
                            messageValue.ValueKind != JsonValueKind.String ||
                            !messageValue.TryGetGuid(out var messageId))
                            throw new ConversationValidationException("A conversationId and messageId are required.");

                        var owner = ConversationAccess.GetOwnerKey(httpContextAccessor.HttpContext?
                            .Request.Headers[ConversationAccess.Header]);
                        var prompt = messages.LastOrDefault(x => x.Role == ChatRole.User)?.Text ?? string.Empty;
                        return conversationRuns.RunAsync(conversationId, owner, messageId, prompt, runId,
                            modelName, effectiveSettings, Generate, cancellationToken);
                    })
            .Build();
    }

}
