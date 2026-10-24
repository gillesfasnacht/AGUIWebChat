using AGUIWebChat.Contracts.Chat;

namespace AGUIWebChat.Server.Services.Chat;

public interface IConversationService
{
    Task<IReadOnlyList<ConversationDto>> ListAsync(string owner, int skip, int take, CancellationToken token);
    Task<ConversationDto> GetAsync(string owner, Guid id, CancellationToken token);
    Task<IReadOnlyList<ChatMessageDto>> GetMessagesAsync(string owner, Guid id, CancellationToken token);
    Task<ConversationDto> CreateAsync(string owner, CancellationToken token);
    Task<ConversationDto> RenameAsync(string owner, Guid id, RenameConversationRequest request, CancellationToken token);
    Task DeleteAsync(string owner, Guid id, CancellationToken token);
}
