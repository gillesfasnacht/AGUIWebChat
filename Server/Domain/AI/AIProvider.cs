using OllamaSharp.Models;

namespace AGUIWebChat.Server.Domain.AI
{
    public sealed class AIProvider
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string ProviderType { get; set; } = string.Empty;

        public string? Endpoint { get; set; }

        public bool IsEnabled { get; set; } = true;

        public ICollection<AIModel> Models { get; set; } = [];
    }
}
