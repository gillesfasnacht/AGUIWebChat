using AGUIWebChat.Server.Hubs;
using Microsoft.Agents.AI;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.AI;
using Serilog;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace AGUIWebChat.Server.Telemetry
{
    static class ReasoningTelemetry
    {
        public const string ActivitySourceName = "AGUIWebChat.Server.ReasoningTelemetry";

        private static readonly ActivitySource ActivitySource = new(ActivitySourceName);

        private static readonly Serilog.ILogger Logger = Log.ForContext("SourceContext", "AGUIWebChat.Server.ReasoningTelemetry");

        public static async IAsyncEnumerable<AgentResponseUpdate> ObserveAsync(
            IEnumerable<ChatMessage> messages,
            AgentSession? session,
            AgentRunOptions? options,
            AIAgent innerAgent,
            IHubContext<TelemetryHub> telemetryHub,
            string? telemetryChannel,
            string runId,
            string agentName,
            string modelName,
            string thinkingLevel,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            Activity? reasoningActivity = null;

            var chunkCount = 0;
            var characterCount = 0;

            try
            {
                await foreach (AgentResponseUpdate update
                    in innerAgent.RunStreamingAsync(
                        messages,
                        session,
                        options,
                        cancellationToken))
                {
                    var containsReasoning = false;

                    foreach (AIContent content in update.Contents)
                    {
                        if (content is TextReasoningContent reasoningContent)
                        {
                            containsReasoning = true;

                            if (reasoningActivity is null)
                            {
                                reasoningActivity = ActivitySource.StartActivity("gen_ai.reasoning", ActivityKind.Internal);

                                if (reasoningActivity is not null)
                                {
                                    reasoningActivity.SetTag("gen_ai.reasoning.effort", thinkingLevel);
                                    reasoningActivity.SetTag("gen_ai.request.model", modelName);
                                    reasoningActivity.SetTag("gen_ai.agent.name", agentName);
                                    reasoningActivity.SetTag("gen_ai.run.id", runId);
                                }
                            }

                            chunkCount++;

                            characterCount += reasoningContent.Text?.Length ?? 0;
                        }
                    }

                    if (!containsReasoning && reasoningActivity is not null)
                    {
                        await CompleteReasoningAsync(
                            reasoningActivity,
                            telemetryHub,
                            telemetryChannel,
                            runId,
                            agentName,
                            modelName,
                            thinkingLevel,
                            chunkCount,
                            characterCount,
                            cancellationToken);

                        reasoningActivity = null;
                    }

                    //if (update.RawRepresentation is not null)
                    //{
                    //    Logger.Information(
                    //        "AgentResponseUpdate.RawRepresentation Type={Type}",
                    //        update.RawRepresentation.GetType().FullName);
                    //}

                    //var chatUpdate = update.AsChatResponseUpdate();

                    //if (chatUpdate.RawRepresentation
                    //    is ChatResponseStream ollamaResponse)
                    //{
                    //    Logger.Information(
                    //        "Ollama Chunk Done={Done} Model={Model} MessageType={MessageType}",
                    //        ollamaResponse.Done,
                    //        ollamaResponse.Model,
                    //        ollamaResponse.Message?.GetType().FullName);

                    //    if (ollamaResponse.Done)
                    //    {
                    //        Logger.Information(
                    //            "FINAL OLLAMA UPDATE ChatUpdate={@ChatUpdate}",
                    //            chatUpdate);

                    //        Logger.Information(
                    //            "FINAL OLLAMA RAW={@Raw}",
                    //            ollamaResponse);
                    //    }

                    //    if (ollamaResponse.Done)
                    //    {
                    //        foreach (var content in update.Contents)
                    //        {
                    //            Logger.Information(
                    //                "FINAL ContentType={ContentType} RawType={RawType}",
                    //                content.GetType().FullName,
                    //                content.RawRepresentation?.GetType().FullName);
                    //        }
                    //    }

                    //    foreach (var content in update.Contents)
                    //    {
                    //        if (content is UsageContent usage)
                    //        {
                    //            Logger.Information(
                    //                "Usage Input={Input} Output={Output} Total={Total} RawType={RawType}",
                    //                usage.Details.InputTokenCount,
                    //                usage.Details.OutputTokenCount,
                    //                usage.Details.TotalTokenCount,
                    //                usage.RawRepresentation?.GetType().FullName);
                    //        }
                    //    }
                    //}

                    yield return update;
                }
            }
            finally
            {
                if (reasoningActivity is not null)
                {
                    await CompleteReasoningAsync(
                        reasoningActivity,
                        telemetryHub,
                            telemetryChannel,
                        runId,
                        agentName,
                        modelName,
                        thinkingLevel,
                        chunkCount,
                        characterCount,
                        cancellationToken);
                }
            }
        }

        private static async Task CompleteReasoningAsync(
            Activity reasoningActivity,
            IHubContext<TelemetryHub> telemetryHub,
            string? telemetryChannel,
            string runId,
            string agentName,
            string modelName,
            string thinkingLevel,
            int chunkCount,
            int characterCount,
            CancellationToken cancellationToken)
        {
            reasoningActivity.SetTag("gen_ai.reasoning.chunk_count", chunkCount);

            reasoningActivity.SetTag("gen_ai.reasoning.character_count", characterCount);

            reasoningActivity.SetStatus(ActivityStatusCode.Ok);

            reasoningActivity.Stop();

            var durationMs =
                reasoningActivity.Duration.TotalMilliseconds;

            Logger.Debug("Reasoning terminé RunId={RunId} Agent={AgentName} Model={ModelName} Effort={ThinkingLevel} Chunks={ChunkCount} Characters={CharacterCount} DurationMs={DurationMs}",
                runId,
                agentName,
                modelName,
                thinkingLevel,
                chunkCount,
                characterCount,
                durationMs);

            await TelemetryPublisher.SendAsync(telemetryHub, telemetryChannel, "ReasoningCompleted",
                new ReasoningTelemetryDto(
                    RunId: runId,
                    DurationMs: durationMs,
                    ChunkCount: chunkCount,
                    CharacterCount: characterCount),
                cancellationToken);

            reasoningActivity.Dispose();
        }
    }
}
