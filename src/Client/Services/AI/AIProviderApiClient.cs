using AGUIWebChat.Contracts.AI.Providers;

namespace AGUIWebChat.Client.Services.AI;

public sealed class AIProviderApiClient
{
    private readonly HttpClient _httpClient;

    public AIProviderApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IReadOnlyList<AIProviderListItem>>
        GetProvidersAsync(CancellationToken cancellationToken = default)
    {
        var providers = await _httpClient
            .GetFromJsonAsync<List<AIProviderListItem>>(
                "/api/providers",
                cancellationToken);

        return providers ?? [];
    }
}
