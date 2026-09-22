namespace AGUIWebChat.Contracts.AI.Models
{
    public sealed class AIModelEditModel
    {
        public int Id { get; set; }

        public int ProviderId { get; set; }

        public string ModelId { get; set; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;

        public ThinkingMode ThinkingMode { get; set; }

        public bool SupportsVision { get; set; }

        public bool SupportsTools { get; set; }

        public bool SupportsStreaming { get; set; }

        public int? ContextWindow { get; set; }

        public int? MaxOutputTokens { get; set; }

        public double? Temperature { get; set; }

        public double? TopP { get; set; }

        public int? TopK { get; set; }

        public int? NumCtx { get; set; }

        public List<ReasoningEffortEditModel> ReasoningEfforts { get; set; } = [];

        public bool IsEnabled { get; set; } = true;
    }
}
