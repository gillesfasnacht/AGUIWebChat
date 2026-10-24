using Microsoft.JSInterop;

namespace AGUIWebChat.Client.Services;

// An anonymous browser capability, not an authenticated user identity.
public sealed class ConversationIdentity(IJSRuntime js)
{
    private readonly SemaphoreSlim _gate = new(1);
    private string? _key;

    public async Task<string> GetKeyAsync(CancellationToken token = default)
    {
        if (_key is not null) return _key;
        await _gate.WaitAsync(token);
        try
        {
            if (_key is not null) return _key;
            var saved = await js.InvokeAsync<string?>("localStorage.getItem", token, "agui-conversation-key");
            if (!Guid.TryParseExact(saved, "N", out var id) || id == Guid.Empty)
            {
                saved = Guid.NewGuid().ToString("N");
                await js.InvokeVoidAsync("localStorage.setItem", token, "agui-conversation-key", saved);
            }
            return _key = saved!;
        }
        finally { _gate.Release(); }
    }
}
