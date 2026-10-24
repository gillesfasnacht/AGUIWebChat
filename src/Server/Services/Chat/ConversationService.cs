using AGUIWebChat.Contracts.Chat;
using AGUIWebChat.Server.Data;
using AGUIWebChat.Server.Domain.Chat;
using Microsoft.EntityFrameworkCore;

namespace AGUIWebChat.Server.Services.Chat;

public sealed class ConversationService(ChatDbContext db) : IConversationService
{
    public async Task<IReadOnlyList<ConversationDto>> ListAsync(string owner, int skip, int take, CancellationToken token)
    {
        if (skip < 0 || take is < 1 or > 100)
            throw new ConversationValidationException("Use skip >= 0 and take between 1 and 100.");

        return await db.ChatConversations.AsNoTracking().Where(x => x.OwnerKey == owner)
            .OrderByDescending(x => x.UpdatedAt).ThenBy(x => x.Id).Skip(skip).Take(take)
            .Select(x => new ConversationDto(x.Id, x.Title, x.CreatedAt, x.UpdatedAt)).ToListAsync(token);
    }

    public async Task<ConversationDto> GetAsync(string owner, Guid id, CancellationToken token) =>
        ToDto(await FindAsync(owner, id, token));

    public async Task<IReadOnlyList<ChatMessageDto>> GetMessagesAsync(string owner, Guid id, CancellationToken token)
    {
        await FindAsync(owner, id, token);

        return await db.ChatMessages.AsNoTracking().Where(x => x.ConversationId == id).OrderBy(x => x.Sequence)
            .Select(x => new ChatMessageDto(x.Id, x.Sequence, x.Role, x.Content, x.Reasoning,
                x.CreatedAt, x.RunId, x.Status)).ToListAsync(token);
    }

    public async Task<ConversationDto> CreateAsync(string owner, CancellationToken token)
    {
        var now = DateTime.UtcNow;
        var conversation = new ChatConversation { Id = Guid.NewGuid(), OwnerKey = owner, CreatedAt = now, UpdatedAt = now };

        db.ChatConversations.Add(conversation);

        await db.SaveChangesAsync(token);

        return ToDto(conversation);
    }

    public async Task<ConversationDto> RenameAsync(string owner, Guid id, RenameConversationRequest request, CancellationToken token)
    {
        var title = request.Title?.Trim();

        if (string.IsNullOrWhiteSpace(title) || title.Length > 200)
            throw new ConversationValidationException("The title is required and cannot exceed 200 characters.");

        var conversation = await FindAsync(owner, id, token);

        await db.ChatConversations.Where(x => x.Id == id && x.OwnerKey == owner)
            .ExecuteUpdateAsync(set => set.SetProperty(x => x.Title, title)
                .SetProperty(x => x.TitleIsCustom, true), token);

        conversation.Title = title;

        return ToDto(conversation);
    }

    public async Task DeleteAsync(string owner, Guid id, CancellationToken token)
    {
        await FindAsync(owner, id, token);

        var now = DateTime.UtcNow;

        var deleted = await db.ChatConversations.Where(x => x.Id == id && x.OwnerKey == owner &&
            (x.ActiveRunId == null || x.ActiveUntil < now)).ExecuteDeleteAsync(token);

        if (deleted == 0) throw new ConversationConflictException("Stop the current response before deleting this conversation.");
    }

    private async Task<ChatConversation> FindAsync(string owner, Guid id, CancellationToken token) =>
        await db.ChatConversations.FirstOrDefaultAsync(x => x.Id == id && x.OwnerKey == owner, token)
        ?? throw new ConversationNotFoundException();

    private static ConversationDto ToDto(ChatConversation x) => new(x.Id, x.Title, x.CreatedAt, x.UpdatedAt);
}
