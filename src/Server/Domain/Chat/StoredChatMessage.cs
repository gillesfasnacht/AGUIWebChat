using AGUIWebChat.Contracts.Chat;

namespace AGUIWebChat.Server.Domain.Chat;

public sealed class StoredChatMessage
{
    public Guid Id { get; set; }
    public Guid ConversationId { get; set; }
    public ChatConversation Conversation { get; set; } = default!;
    public int Sequence { get; set; }
    public ChatMessageRole Role { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? Reasoning { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? RunId { get; set; }
    public ChatMessageStatus Status { get; set; }
    public string? ModelName { get; set; }
    public string? SettingsJson { get; set; }
}
