using AGUIWebChat.Client.Models;
using AGUIWebChat.Client.Services;
using AGUIWebChat.Contracts.Chat;
using Microsoft.JSInterop;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Xunit;

namespace AGUIWebChat.Client.Tests;

public sealed class ConversationClientTests
{
    [Fact]
    public async Task BrowserKey_ShouldSurviveNewServiceInstances()
    {
        var js = new StorageJs();
        var key = await new ConversationIdentity(js).GetKeyAsync(TestContext.Current.CancellationToken);
        Assert.True(Guid.TryParseExact(key, "N", out _));
        Assert.Equal(key, await new ConversationIdentity(js).GetKeyAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ConversationApi_ShouldSendBrowserKeyAndReadStoredStatuses()
    {
        var conversation = Guid.NewGuid();
        var js = new StorageJs();
        var identity = new ConversationIdentity(js);
        string? header = null;
        using var http = new HttpClient(new Handler(async request =>
        {
            header = request.Headers.GetValues("X-Conversation-Key").Single();
            await Task.Yield();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new[] { new
                {
                    id = Guid.NewGuid(), sequence = 1, role = "Assistant", content = "Partial",
                    reasoning = (string?)null, createdAt = DateTime.UtcNow,
                    runId = "run", status = "Cancelled"
                } })
            };
        })) { BaseAddress = new Uri("http://localhost") };
        var messages = await new ConversationApiClient(http, identity).MessagesAsync(conversation, TestContext.Current.CancellationToken);
        Assert.Equal(await identity.GetKeyAsync(TestContext.Current.CancellationToken), header);
        var message = Assert.Single(messages);
        Assert.Equal(ChatMessageStatus.Cancelled, message.Status);
        Assert.Equal("Partial", message.Content);
    }

    [Fact]
    public async Task AgUiRequests_ShouldCarryConversationAndUseFreshSessions()
    {
        var requests = new List<(Guid Conversation, Guid Message, string Thread, string Key)>();
        var js = new StorageJs();
        var identity = new ConversationIdentity(js);
        using var http = new HttpClient(new Handler(async request =>
        {
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
            var root = body.RootElement;
            var state = root.GetProperty("state");
            var threadId = root.GetProperty("threadId").GetString()!;
            var runId = root.GetProperty("runId").GetString()!;
            requests.Add((state.GetProperty("conversationId").GetGuid(), state.GetProperty("messageId").GetGuid(),
                threadId, request.Headers.GetValues("X-Conversation-Key").Single()));
            var events = new object[]
            {
                new { type = "RUN_STARTED", threadId, runId },
                new { type = "TEXT_MESSAGE_START", messageId = "assistant", role = "assistant" },
                new { type = "TEXT_MESSAGE_CONTENT", messageId = "assistant", delta = "Answer" },
                new { type = "TEXT_MESSAGE_END", messageId = "assistant" },
                new { type = "RUN_FINISHED", threadId, runId }
            };
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(string.Concat(events.Select(e => "data: " + JsonSerializer.Serialize(e) + "\n\n")),
                    Encoding.UTF8, "text/event-stream")
            };
        })) { BaseAddress = new Uri("http://localhost") };
        var telemetry = new TelemetryService("http://localhost/telemetry");
        var chat = new AgUiChatService(http, telemetry, identity);
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        foreach (var conversation in new[] { first, second })
            await foreach (var _ in chat.SendAsync("Question", conversation, Guid.NewGuid(), new ChatSettings(),
                TestContext.Current.CancellationToken)) { }
        Assert.Equal(first, requests[0].Conversation);
        Assert.Equal(second, requests[1].Conversation);
        Assert.NotEqual(requests[0].Thread, requests[1].Thread);
        Assert.NotEqual(requests[0].Message, requests[1].Message);
        Assert.Equal(await identity.GetKeyAsync(TestContext.Current.CancellationToken), requests[0].Key);
    }

    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> action) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => action(request);
    }

    private sealed class StorageJs : IJSRuntime
    {
        private string? _key;
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            InvokeAsync<TValue>(identifier, CancellationToken.None, args);
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken token, object?[]? args)
        {
            if (identifier == "localStorage.getItem") return ValueTask.FromResult((TValue)(object?)_key!);
            if (identifier == "localStorage.setItem") _key = (string)args![1]!;
            return ValueTask.FromResult(default(TValue)!);
        }
    }
}

