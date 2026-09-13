using AGUIFluentUIChatClient.Services;

namespace AGUIFluentUIChatClient.Models
{
    public enum ChatRole
    {
        User,
        Assistant,
        System
    }

    public sealed class ChatMessageModel
    {
        public required string Id { get; init; }
        public ChatRole Role { get; init; }

        public string Content { get; set; } = string.Empty;
        public string? Reasoning { get; set; }

        public string? RunId { get; set; }
        public double? ReasoningDurationMs { get; set; }
        public int? ReasoningChunkCount { get; set; }
        public int? ReasoningCharacterCount { get; set; }

        public bool IsStreaming { get; set; }

        public LlmTelemetryDto? MonitoringTelemetry { get; set; }
    }
}
