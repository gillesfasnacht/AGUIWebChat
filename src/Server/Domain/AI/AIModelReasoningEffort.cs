namespace AGUIWebChat.Server.Domain.AI
{
    public sealed class AIModelReasoningEffort
    {
        public int Id { get; set; }

        public int AIModelId { get; set; }

        public AIModel AIModel { get; set; } = null!;

        // Texte présenté à l'utilisateur
        public string DisplayName { get; set; } = string.Empty;

        // Valeur réellement transmise au provider
        public string Value { get; set; } = string.Empty;

        public int SortOrder { get; set; }

        public bool IsDefault { get; set; }
    }
}
