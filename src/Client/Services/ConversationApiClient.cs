using AGUIWebChat.Client.Services.AI;
using AGUIWebChat.Contracts.Chat;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AGUIWebChat.Client.Services;

public sealed class ConversationApiClient(HttpClient http, ConversationIdentity identity)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, object? body, CancellationToken token)
    {
        using var request = new HttpRequestMessage(method, "/api/conversations" + path);
        request.Headers.Add("X-Conversation-Key", await identity.GetKeyAsync(token));
        if (body is not null) request.Content = JsonContent.Create(body, options: JsonOptions);
        var response = await http.SendAsync(request, token);
        try { await ApiResponse.EnsureSuccessAsync(response, token); return response; }
        catch { response.Dispose(); throw; }
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response, CancellationToken token) =>
        await response.Content.ReadFromJsonAsync<T>(JsonOptions, token)
        ?? throw new InvalidOperationException("The API returned an empty conversation response.");

    public async Task<IReadOnlyList<ConversationDto>> ListAsync(int skip = 0, CancellationToken token = default)
    {
        using var response = await SendAsync(HttpMethod.Get, $"?skip={skip}&take=30", null, token);
        return await ReadAsync<List<ConversationDto>>(response, token);
    }
    public async Task<ConversationDto> GetAsync(Guid id, CancellationToken token = default)
    {
        using var response = await SendAsync(HttpMethod.Get, $"/{id}", null, token);
        return await ReadAsync<ConversationDto>(response, token);
    }
    public async Task<IReadOnlyList<ChatMessageDto>> MessagesAsync(Guid id, CancellationToken token = default)
    {
        using var response = await SendAsync(HttpMethod.Get, $"/{id}/messages", null, token);
        return await ReadAsync<List<ChatMessageDto>>(response, token);
    }
    public async Task<ConversationDto> CreateAsync(CancellationToken token = default)
    {
        using var response = await SendAsync(HttpMethod.Post, "", null, token);
        return await ReadAsync<ConversationDto>(response, token);
    }
    public async Task<ConversationDto> RenameAsync(Guid id, string title, CancellationToken token = default)
    {
        using var response = await SendAsync(HttpMethod.Patch, $"/{id}", new RenameConversationRequest { Title = title }, token);
        return await ReadAsync<ConversationDto>(response, token);
    }
    public async Task DeleteAsync(Guid id, CancellationToken token = default)
    {
        using var response = await SendAsync(HttpMethod.Delete, $"/{id}", null, token);
    }
}
