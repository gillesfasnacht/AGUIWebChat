namespace AGUIWebChat.Contracts.AI.Models
{
    public sealed class ReasoningEffortEditModel
    {
        public int Id { get; set; }

        public string DisplayName { get; set; } = string.Empty;

        public string Value { get; set; } = string.Empty;

        public int SortOrder { get; set; }

        public bool IsDefault { get; set; }
    }
}
