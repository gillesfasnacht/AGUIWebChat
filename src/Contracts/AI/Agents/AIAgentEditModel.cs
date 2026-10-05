namespace AGUIWebChat.Contracts.AI.Agents
{
    public sealed class AIAgentEditModel
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        public string SystemPrompt { get; set; } = string.Empty;

        public int AIModelId { get; set; }

        public string AIModelDisplayName { get; set; } = string.Empty;

        // Runtime overrides
        public double? Temperature { get; set; }

        public double? TopP { get; set; }

        public int? TopK { get; set; }

        public int? NumCtx { get; set; }

        public string? ReasoningEffort { get; set; }

        public bool IsEnabled { get; set; } = true;
    }
}
