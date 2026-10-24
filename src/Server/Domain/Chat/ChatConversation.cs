namespace AGUIWebChat.Server.Domain.Chat;

public sealed class ChatConversation
{
    public Guid Id { get; set; }
    public string OwnerKey { get; set; } = string.Empty;
    public string Title { get; set; } = "New conversation";
    public bool TitleIsCustom { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid? ActiveRunId { get; set; }
    public DateTime? ActiveUntil { get; set; }
    public List<StoredChatMessage> Messages { get; set; } = [];
}
