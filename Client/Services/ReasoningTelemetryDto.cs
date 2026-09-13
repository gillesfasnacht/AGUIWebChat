namespace AGUIFluentUIChatClient.Services
{
    public sealed class ReasoningTelemetryDto
    {
        public string RunId { get; set; } = string.Empty;
        public double DurationMs { get; set; }
        public int ChunkCount { get; set; }
        public int CharacterCount { get; set; }
    }
}
