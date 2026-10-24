using AGUIWebChat.Contracts.Chat;
using AGUIWebChat.Server.Data;
using AGUIWebChat.Server.Inference;
using AGUIWebChat.Server.Services.Chat;
using AGUIWebChat.Server.Tests.Infrastructure;
using Microsoft.Agents.AI;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using System.Runtime.CompilerServices;
using Xunit;

namespace AGUIWebChat.Server.Tests.Services.Chat;

public sealed class ConversationRunTests
{
    [Fact]
    public async Task Runs_ShouldRestoreStoredContextAndNeverMixConversations()
    {
        var token = TestContext.Current.CancellationToken;
        await using var database = await TestDbContextFactory.CreateAsync(token);
        await using var services = Services(database.DbContext);
        var runner = Runner(services);
        var catalogue = new ConversationService(database.DbContext);
        var first = await catalogue.CreateAsync("owner", token);
        var second = await catalogue.CreateAsync("owner", token);
        var histories = new List<string[]>();
        async IAsyncEnumerable<AgentResponseUpdate> Generate(IReadOnlyList<ChatMessage> history,
            [EnumeratorCancellation] CancellationToken ct)
        {
            histories.Add(history.Select(x => x.Text).ToArray());
            await Task.Yield();
            yield return new AgentResponseUpdate(ChatRole.Assistant, "Answer");
        }
        await Consume(runner.RunAsync(first.Id, "owner", Guid.NewGuid(), "First question", "run1",
            "test-model", new InferenceSettings(), Generate, token));
        await Consume(runner.RunAsync(first.Id, "owner", Guid.NewGuid(), "Follow up", "run2",
            "test-model", new InferenceSettings(), Generate, token));
        await Consume(runner.RunAsync(second.Id, "owner", Guid.NewGuid(), "Other question", "run3",
            "test-model", new InferenceSettings(), Generate, token));
        Assert.Equal(["First question"], histories[0]);
        Assert.Equal(["First question", "Answer", "Follow up"], histories[1]);
        Assert.Equal(["Other question"], histories[2]);
        var persisted = await database.DbContext.ChatMessages.AsNoTracking().Where(x => x.ConversationId == first.Id)
            .OrderBy(x => x.Sequence).ToListAsync(token);
        Assert.Equal(4, persisted.Count);
        Assert.All(persisted, x => Assert.Equal(ChatMessageStatus.Completed, x.Status));
        Assert.Equal("test-model", persisted[1].ModelName);
        Assert.Contains("Temperature", persisted[1].SettingsJson);
        var title = await database.DbContext.ChatConversations.AsNoTracking().SingleAsync(x => x.Id == first.Id, token);
        Assert.Equal("First question", title.Title);
        Assert.Null(title.ActiveRunId);
        await catalogue.DeleteAsync("owner", first.Id, token);
        Assert.False(await database.DbContext.ChatMessages.AnyAsync(x => x.ConversationId == first.Id, token));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task InterruptedRun_ShouldSavePartialTextAndReleaseLease(bool cancel)
    {
        var token = TestContext.Current.CancellationToken;
        await using var database = await TestDbContextFactory.CreateAsync(token);
        await using var services = Services(database.DbContext);
        var conversation = await new ConversationService(database.DbContext).CreateAsync("owner", token);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(token);
        async IAsyncEnumerable<AgentResponseUpdate> Generate(IReadOnlyList<ChatMessage> history,
            [EnumeratorCancellation] CancellationToken ct)
        {
            yield return new AgentResponseUpdate(ChatRole.Assistant, "Partial answer");
            await Task.Yield();
            if (cancel) { cancellation.Cancel(); ct.ThrowIfCancellationRequested(); }
            throw new InvalidOperationException("Inference unavailable.");
        }
        var stream = Runner(services).RunAsync(conversation.Id, "owner", Guid.NewGuid(), "Question", "run",
            "model", new InferenceSettings(), Generate, cancellation.Token);
        if (cancel) await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Consume(stream));
        else await Assert.ThrowsAsync<InvalidOperationException>(() => Consume(stream));
        var answer = await database.DbContext.ChatMessages.AsNoTracking()
            .SingleAsync(x => x.Role == ChatMessageRole.Assistant, token);
        Assert.Equal("Partial answer", answer.Content);
        Assert.Equal(cancel ? ChatMessageStatus.Cancelled : ChatMessageStatus.Failed, answer.Status);
        var stored = await database.DbContext.ChatConversations.AsNoTracking().SingleAsync(token);
        Assert.Null(stored.ActiveRunId);
    }

    [Fact]
    public async Task ActiveRun_ShouldRejectConcurrentSendAndDeletion()
    {
        var token = TestContext.Current.CancellationToken;
        await using var database = await TestDbContextFactory.CreateAsync(token);
        await using var services = Services(database.DbContext);
        var catalogue = new ConversationService(database.DbContext);
        var conversation = await catalogue.CreateAsync("owner", token);
        var runner = Runner(services);
        var messageId = Guid.NewGuid();
        async IAsyncEnumerable<AgentResponseUpdate> Generate(IReadOnlyList<ChatMessage> history,
            [EnumeratorCancellation] CancellationToken ct)
        {
            await Task.Yield();
            yield return new AgentResponseUpdate(ChatRole.Assistant, "Start");
        }
        await using (var active = runner.RunAsync(conversation.Id, "owner", messageId, "Question", "run",
            "model", new InferenceSettings(), Generate, token).GetAsyncEnumerator(token))
        {
            Assert.True(await active.MoveNextAsync());
            await Assert.ThrowsAsync<ConversationConflictException>(() => Consume(runner.RunAsync(
                conversation.Id, "owner", Guid.NewGuid(), "Second", "run2", "model", new(), Generate, token)));
            await Assert.ThrowsAsync<ConversationConflictException>(() => catalogue.DeleteAsync("owner", conversation.Id, token));
        }
        await Assert.ThrowsAsync<ConversationConflictException>(() => Consume(runner.RunAsync(
            conversation.Id, "owner", messageId, "Question", "run", "model", new(), Generate, token)));
        await Assert.ThrowsAsync<ConversationNotFoundException>(() => Consume(runner.RunAsync(
            conversation.Id, "other-owner", Guid.NewGuid(), "Question", "run", "model", new(), Generate, token)));
    }

    private static ServiceProvider Services(ChatDbContext db) => new ServiceCollection()
        .AddDbContext<ChatDbContext>(options => options.UseSqlite(db.Database.GetDbConnection()))
        .BuildServiceProvider();

    private static ConversationRunCoordinator Runner(ServiceProvider services) =>
        new(services.GetRequiredService<IServiceScopeFactory>(), NullLogger<ConversationRunCoordinator>.Instance);

    private static async Task Consume(IAsyncEnumerable<AgentResponseUpdate> stream)
    {
        await foreach (var _ in stream) { }
    }
}
