using AGUIWebChat.Contracts.Chat;
using AGUIWebChat.Server.Services.Chat;
using AGUIWebChat.Server.Tests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace AGUIWebChat.Server.Tests.Services.Endpoints;

public sealed class ConversationStreamingTests
{
    [Fact]
    public async Task AgUi_ShouldStreamPersistAndRestoreHistoryThroughActualEndpoint()
    {
        var token = TestContext.Current.CancellationToken;
        var inference = new FakeChatClient();
        await using var factory = new AGUIWebChatWebApplicationFactory();
        using var configured = factory.WithWebHostBuilder(builder => builder.ConfigureServices(
            services => services.AddSingleton<IChatClient>(inference)));
        using var client = configured.CreateClient();
        client.DefaultRequestHeaders.Add(ConversationAccess.Header, Guid.NewGuid().ToString("N"));
        using var createdResponse = await client.PostAsync("/api/conversations", null, token);
        var conversation = await createdResponse.Content.ReadFromJsonAsync<ConversationDto>(token);
        Assert.NotNull(conversation);
        foreach (var question in new[] { "Hello", "Remember my question?" })
        {
            using var response = await client.PostAsJsonAsync("/ag-ui", new
            {
                threadId = conversation.Id.ToString(), runId = Guid.NewGuid().ToString("N"),
                messages = new[] { new { id = Guid.NewGuid().ToString(), role = "user", content = question } },
                state = new { conversationId = conversation.Id, messageId = Guid.NewGuid() },
                tools = Array.Empty<object>(), context = Array.Empty<object>(), forwardedProps = new { }
            }, token);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var events = await response.Content.ReadAsStringAsync(token);
            Assert.Contains("TEXT_MESSAGE_CONTENT", events);
            Assert.DoesNotContain("RUN_ERROR", events);
        }
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            Converters = { new JsonStringEnumConverter() }
        };
        var messages = await client.GetFromJsonAsync<List<ChatMessageDto>>(
            $"/api/conversations/{conversation.Id}/messages", options, token);
        Assert.NotNull(messages);
        Assert.Equal(4, messages.Count);
        Assert.Equal("Hello", messages[0].Content);
        Assert.Equal("Test answer", messages[1].Content);
        Assert.All(messages, x => Assert.Equal(ChatMessageStatus.Completed, x.Status));
        Assert.Equal(2, inference.Histories.Count);
        Assert.Contains("Hello", inference.Histories[1]);
        Assert.Contains("Test answer", inference.Histories[1]);
        Assert.Contains("Remember my question?", inference.Histories[1]);
    }

    private sealed class FakeChatClient : IChatClient
    {
        public List<string[]> Histories { get; } = [];
        public void Dispose() { }
        public object? GetService(Type serviceType, object? serviceKey = null) =>
            serviceType == typeof(ChatClientMetadata) ? new ChatClientMetadata("Test") : null;
        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages,
            ChatOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            Histories.Add(messages.Select(x => x.Text).ToArray());
            await Task.Yield();
            yield return new ChatResponseUpdate(ChatRole.Assistant, "Test answer")
            {
                MessageId = Guid.NewGuid().ToString(), ResponseId = Guid.NewGuid().ToString(),
                FinishReason = ChatFinishReason.Stop
            };
        }
    }
}

