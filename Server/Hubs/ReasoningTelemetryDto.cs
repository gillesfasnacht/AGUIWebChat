namespace AGUIWebChatServer.Hubs
{
    public sealed record ReasoningTelemetryDto(
        string RunId,
        double DurationMs,
        int ChunkCount,
        int CharacterCount);
}
