namespace AGUIWebChatServer.Telemetry
{
    public sealed record LlmTelemetryDto(
        string RunId,
        string AgentName,
        string Model,

        long InputTokens,
        long OutputTokens,
        long TotalTokens,

        double RequestDurationMs,
        double EndToEndTokensPerSecond,

        double? OllamaTotalDurationMs,
        double? LoadDurationMs,
        double? PromptEvalDurationMs,
        double? EvalDurationMs,

        double? PromptTokensPerSecond,
        double? GenerationTokensPerSecond);
}
