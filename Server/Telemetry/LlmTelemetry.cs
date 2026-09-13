using AGUIWebChatServer.Hubs;
using AGUIWebChatServer.Telemetry;

using Microsoft.Agents.AI;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.AI;

using OllamaSharp.Models.Chat;

using Serilog;

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace AGUIWebChatServer.Telemetry
{
    internal static class LlmTelemetry
    {
        private static readonly Serilog.ILogger Logger = Log.ForContext("SourceContext", "AGUIWebChatServer.LlmTelemetry");

        public static async IAsyncEnumerable<AgentResponseUpdate> ObserveAsync(
            IAsyncEnumerable<AgentResponseUpdate> updates,
            IHubContext<TelemetryHub> telemetryHub,
            string? telemetryChannel,
            string runId,
            string agentName,
            string modelName,
            [EnumeratorCancellation]
            CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();

            long inputTokens = 0;
            long outputTokens = 0;
            long totalTokens = 0;

            OllamaMetrics? ollamaMetrics = null;

            try
            {
                await foreach (var update in updates
                    .WithCancellation(cancellationToken))
                {
                    //
                    // 1. Métriques génériques Microsoft.Extensions.AI
                    //
                    foreach (var content in update.Contents)
                    {
                        if (content is UsageContent usageContent)
                        {
                            inputTokens = usageContent.Details.InputTokenCount ?? 0;

                            outputTokens = usageContent.Details.OutputTokenCount ?? 0;

                            totalTokens = usageContent.Details.TotalTokenCount ?? 0;
                        }
                    }

                    //
                    // 2. Métriques spécifiques Ollama
                    //
                    var chatUpdate = update.AsChatResponseUpdate();

                    if (chatUpdate.RawRepresentation
                        is ChatDoneResponseStream ollamaResponse)
                    {
                        ollamaMetrics =
                            new OllamaMetrics(
                                ollamaResponse.TotalDuration,
                                ollamaResponse.LoadDuration,
                                ollamaResponse.PromptEvalCount,
                                ollamaResponse.PromptEvalDuration,
                                ollamaResponse.EvalCount,
                                ollamaResponse.EvalDuration);
                    }

                    yield return update;
                }
            }
            finally
            {
                stopwatch.Stop();

                var requestDurationMs = stopwatch.Elapsed.TotalMilliseconds;

                var endToEndTokensPerSecond =
                    requestDurationMs > 0
                        ? outputTokens /
                          (requestDurationMs / 1000.0)
                        : 0;

                var telemetry =
                    new LlmTelemetryDto(
                        RunId: runId,
                        AgentName: agentName,
                        Model: modelName,

                        InputTokens: inputTokens,
                        OutputTokens: outputTokens,
                        TotalTokens: totalTokens,

                        RequestDurationMs: requestDurationMs,
                        EndToEndTokensPerSecond: endToEndTokensPerSecond,

                        OllamaTotalDurationMs: ollamaMetrics?.TotalDurationMs,
                        LoadDurationMs: ollamaMetrics?.LoadDurationMs,
                        PromptEvalDurationMs: ollamaMetrics?.PromptEvalDurationMs,
                        EvalDurationMs: ollamaMetrics?.EvalDurationMs,
                        PromptTokensPerSecond: ollamaMetrics?.PromptTokensPerSecond,
                        GenerationTokensPerSecond: ollamaMetrics?.GenerationTokensPerSecond);

                Logger.Information(
                    "LLM completed RunId={RunId} Agent={AgentName} Model={Model} InputTokens={InputTokens} OutputTokens={OutputTokens} TotalTokens={TotalTokens} RequestDurationMs={RequestDurationMs} OllamaDurationMs={OllamaDurationMs} GenerationTokensPerSecond={GenerationTokensPerSecond}",
                    telemetry.RunId,
                    telemetry.AgentName,
                    telemetry.Model,
                    telemetry.InputTokens,
                    telemetry.OutputTokens,
                    telemetry.TotalTokens,
                    telemetry.RequestDurationMs,
                    telemetry.OllamaTotalDurationMs,
                    telemetry.GenerationTokensPerSecond);

                await TelemetryPublisher.SendAsync(telemetryHub, telemetryChannel,
                    "LlmMetricsCompleted",
                    telemetry,
                    cancellationToken);
            }
        }
    }
}
