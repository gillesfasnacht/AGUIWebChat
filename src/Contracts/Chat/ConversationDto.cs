using System.ComponentModel.DataAnnotations;

namespace AGUIWebChat.Contracts.Chat;

public enum ChatMessageRole { User, Assistant }
public enum ChatMessageStatus { InProgress, Completed, Cancelled, Failed }

public sealed record ConversationDto(Guid Id, string Title, DateTime CreatedAt, DateTime UpdatedAt);
public sealed class RenameConversationRequest
{
    [Required, StringLength(200)]
    public string Title { get; set; } = string.Empty;
}
public sealed record ChatMessageDto(Guid Id, int Sequence, ChatMessageRole Role, string Content,
    string? Reasoning, DateTime CreatedAt, string? RunId, ChatMessageStatus Status);
