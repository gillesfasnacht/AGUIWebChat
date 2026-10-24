using System.Runtime.CompilerServices;
using System.Text.Json;
using AGUIWebChat.Contracts.Chat;
using AGUIWebChat.Server.Data;
using AGUIWebChat.Server.Domain.Chat;
using AGUIWebChat.Server.Inference;
using Microsoft.Agents.AI;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;

namespace AGUIWebChat.Server.Services.Chat;

// Singleton coordinator; each generation owns its DI scope and DbContext.
public sealed class ConversationRunCoordinator(IServiceScopeFactory scopeFactory, ILogger<ConversationRunCoordinator> logger)
{
    public async IAsyncEnumerable<AgentResponseUpdate> RunAsync(
        Guid conversationId, string ownerKey, Guid userMessageId, string prompt, string runId,
        string modelName, InferenceSettings settings,
        Func<IReadOnlyList<ChatMessage>, CancellationToken, IAsyncEnumerable<AgentResponseUpdate>> generate,
        [EnumeratorCancellation] CancellationToken token)
    {
        if (userMessageId == Guid.Empty || string.IsNullOrWhiteSpace(prompt))
            throw new ConversationValidationException("A message identifier and non-empty message are required.");

        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();
        var now = DateTime.UtcNow;
        var leaseId = Guid.NewGuid();
        var changed = await db.ChatConversations.Where(x => x.Id == conversationId && x.OwnerKey == ownerKey &&
                (x.ActiveRunId == null || x.ActiveUntil < now))
            .ExecuteUpdateAsync(set => set.SetProperty(x => x.ActiveRunId, leaseId)
                .SetProperty(x => x.ActiveUntil, now.AddMinutes(2)), token);
        if (changed == 0)
        {
            if (!await db.ChatConversations.AnyAsync(x => x.Id == conversationId && x.OwnerKey == ownerKey, token))
                throw new ConversationNotFoundException();
            throw new ConversationConflictException("A response is already running for this conversation.");
        }

        using var runCancellation = CancellationTokenSource.CreateLinkedTokenSource(token);
        using var heartbeatStop = new CancellationTokenSource();
        var heartbeat = RenewLeaseAsync(conversationId, leaseId, runCancellation, heartbeatStop.Token);
        StoredChatMessage? assistant = null;
        var completed = false;
        try
        {
            var conversation = await db.ChatConversations.SingleAsync(x => x.Id == conversationId, token);
            var stored = await db.ChatMessages.Where(x => x.ConversationId == conversationId)
                .OrderBy(x => x.Sequence).ToListAsync(token);
            if (stored.Any(x => x.Id == userMessageId))
                throw new ConversationConflictException("This message has already been submitted.");
            // Recover an abandoned lease following a process shutdown.
            foreach (var old in stored.Where(x => x.Status == ChatMessageStatus.InProgress))
                old.Status = ChatMessageStatus.Failed;

            var history = stored.Where(x => x.Role == ChatMessageRole.User ||
                    (x.Status is ChatMessageStatus.Completed or ChatMessageStatus.Cancelled && x.Content.Length > 0))
                .Select(x => new ChatMessage(x.Role == ChatMessageRole.User ? ChatRole.User : ChatRole.Assistant, x.Content))
                .ToList();
            history.Add(new ChatMessage(ChatRole.User, prompt));

            var sequence = stored.Count == 0 ? 0 : stored[^1].Sequence;
            db.ChatMessages.Add(new StoredChatMessage
            {
                Id = userMessageId, ConversationId = conversationId, Sequence = sequence + 1,
                Role = ChatMessageRole.User, Content = prompt, CreatedAt = now, Status = ChatMessageStatus.Completed
            });
            assistant = new StoredChatMessage
            {
                Id = Guid.NewGuid(), ConversationId = conversationId, Sequence = sequence + 2,
                Role = ChatMessageRole.Assistant, CreatedAt = now, RunId = runId,
                Status = ChatMessageStatus.InProgress, ModelName = modelName,
                SettingsJson = JsonSerializer.Serialize(settings)
            };
            db.ChatMessages.Add(assistant);
            if (stored.Count == 0 && !conversation.TitleIsCustom)
                conversation.Title = prompt.Length <= 80 ? prompt : prompt[..80] + "…";
            conversation.UpdatedAt = now;
            await db.SaveChangesAsync(token);

            var checkpoint = DateTime.UtcNow;
            await foreach (var update in generate(history, runCancellation.Token).WithCancellation(runCancellation.Token))
            {
                assistant.Content += update.Text;
                foreach (var reasoning in update.Contents.OfType<TextReasoningContent>())
                    assistant.Reasoning = (assistant.Reasoning ?? string.Empty) + reasoning.Text;
                if (DateTime.UtcNow - checkpoint >= TimeSpan.FromSeconds(2))
                {
                    checkpoint = DateTime.UtcNow;
                    conversation.ActiveUntil = checkpoint.AddMinutes(2);
                    conversation.UpdatedAt = checkpoint;
                    await db.SaveChangesAsync(token);
                }
                yield return update;
            }
            completed = true;
        }
        finally
        {
            await heartbeatStop.CancelAsync();
            await heartbeat;
            // RequestAborted must not cancel saving the partial response.
            using var saveTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            try
            {
                // A failed insert/claim must never be retried accidentally during cleanup.
                if (assistant is not null && db.Entry(assistant).State != EntityState.Added)
                {
                    assistant.Status = completed ? ChatMessageStatus.Completed
                        : token.IsCancellationRequested ? ChatMessageStatus.Cancelled : ChatMessageStatus.Failed;
                    await db.SaveChangesAsync(saveTimeout.Token);
                }
            }
            finally
            {
                try
                {
                    await db.ChatConversations.Where(x => x.Id == conversationId && x.ActiveRunId == leaseId)
                        .ExecuteUpdateAsync(set => set.SetProperty(x => x.ActiveRunId, (Guid?)null)
                            .SetProperty(x => x.ActiveUntil, (DateTime?)null)
                            .SetProperty(x => x.UpdatedAt, DateTime.UtcNow), saveTimeout.Token);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Could not release conversation generation {ConversationId}", conversationId);
                }
            }
        }
    }
    private async Task RenewLeaseAsync(Guid conversationId, Guid leaseId,
        CancellationTokenSource generation, CancellationToken stop)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        try
        {
            while (await timer.WaitForNextTickAsync(stop))
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();
                var updated = await db.ChatConversations.Where(x => x.Id == conversationId && x.ActiveRunId == leaseId)
                    .ExecuteUpdateAsync(set => set.SetProperty(x => x.ActiveUntil, DateTime.UtcNow.AddMinutes(2)), stop);
                if (updated == 0) { await generation.CancelAsync(); return; }
            }
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not renew conversation generation {ConversationId}", conversationId);
            await generation.CancelAsync();
        }
    }

}

