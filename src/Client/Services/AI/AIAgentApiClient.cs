using System.Net;
using AGUIWebChat.Contracts.AI.Agents;

namespace AGUIWebChat.Client.Services.AI
{
    public sealed class AIAgentApiClient
    {
        private readonly HttpClient _httpClient;

        public AIAgentApiClient(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<IReadOnlyList<AIAgentEditModel>> GetAgentsAsync(CancellationToken cancellationToken = default)
        {
            using var response = await _httpClient.GetAsync("/api/agents", cancellationToken);

            await ApiResponse.EnsureSuccessAsync(response, cancellationToken);

            var agents = await response.Content.ReadFromJsonAsync<List<AIAgentEditModel>>(cancellationToken);

            return agents ?? [];
        }

        public async Task<AIAgentEditModel?> GetAgentAsync(int id, CancellationToken cancellationToken = default)
        {
            using var response = await _httpClient.GetAsync($"/api/agents/{id}", cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound) return null;

            await ApiResponse.EnsureSuccessAsync(response, cancellationToken);

            return await response.Content.ReadFromJsonAsync<AIAgentEditModel>(cancellationToken);
        }

        public async Task<AIAgentEditModel> CreateAsync(AIAgentEditModel agent, CancellationToken cancellationToken = default)
        {
            using var response = await _httpClient.PostAsJsonAsync("/api/agents", agent, cancellationToken);

            await ApiResponse.EnsureSuccessAsync(response, cancellationToken);

            return await response.Content.ReadFromJsonAsync<AIAgentEditModel>(cancellationToken)
                   ?? throw new InvalidOperationException("The server returned an empty agent.");
        }

        public async Task<AIAgentEditModel> UpdateAsync(AIAgentEditModel agent, CancellationToken cancellationToken = default)
        {
            using var response = await _httpClient.PutAsJsonAsync($"/api/agents/{agent.Id}", agent, cancellationToken);

            await ApiResponse.EnsureSuccessAsync(response, cancellationToken);

            return await response.Content.ReadFromJsonAsync<AIAgentEditModel>(cancellationToken) 
                ?? throw new InvalidOperationException("The server returned an empty agent.");
        }

        public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
        {
            using var response = await _httpClient.DeleteAsync($"/api/agents/{id}", cancellationToken);

            await ApiResponse.EnsureSuccessAsync(response, cancellationToken);
        }
    }
}
