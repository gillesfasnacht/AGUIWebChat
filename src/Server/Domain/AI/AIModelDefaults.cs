namespace AGUIWebChat.Server.Domain.AI
{
    public sealed class AIModelDefaults
    {
        public int Id { get; set; }

        public int AIModelId { get; set; }

        public AIModel AIModel { get; set; } = null!;

        public double? Temperature { get; set; }

        public double? TopP { get; set; }

        public int? TopK { get; set; }

        public int? NumCtx { get; set; }
    }
}
