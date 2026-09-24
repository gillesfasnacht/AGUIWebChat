using AGUIWebChat.Contracts.AI;

namespace AGUIWebChat.Server.Domain.AI
{
    public sealed class AIModel
    {
        public int Id { get; set; }

        // Provider
        public int ProviderId { get; set; }

        public AIProvider Provider { get; set; } = null!;

        // Identification
        public string ModelId { get; set; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;

        // Reasoning
        public ThinkingMode ThinkingMode { get; set; }

        // Capabilities
        public bool SupportsVision { get; set; }

        public bool SupportsTools { get; set; }

        public bool SupportsStreaming { get; set; }

        // Model limits
        public int? ContextWindow { get; set; }

        public int? MaxOutputTokens { get; set; }

        // Runtime defaults
        public AIModelDefaults? Defaults { get; set; }

        // Dynamic reasoning levels
        public ICollection<AIModelReasoningEffort> ReasoningEfforts { get; set; } = [];

        public bool IsEnabled { get; set; } = true;
    }
}
