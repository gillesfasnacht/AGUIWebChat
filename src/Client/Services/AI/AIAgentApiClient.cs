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
            var agents = await _httpClient.GetFromJsonAsync<List<AIAgentEditModel>>("/api/agents", cancellationToken);

            return agents ?? [];
        }

        public async Task<AIAgentEditModel?> GetAgentAsync(int id, CancellationToken cancellationToken = default)
        {
            return await _httpClient.GetFromJsonAsync<AIAgentEditModel>($"/api/agents/{id}", cancellationToken);
        }

        public async Task<AIAgentEditModel> CreateAsync(AIAgentEditModel agent, CancellationToken cancellationToken = default)
        {
            using var response = await _httpClient.PostAsJsonAsync("/api/agents", agent, cancellationToken);

            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<AIAgentEditModel>(cancellationToken)
                   ?? throw new InvalidOperationException("The server returned an empty agent.");
        }

        public async Task<AIAgentEditModel> UpdateAsync(AIAgentEditModel agent, CancellationToken cancellationToken = default)
        {
            using var response = await _httpClient.PutAsJsonAsync($"/api/agents/{agent.Id}", agent, cancellationToken);

            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<AIAgentEditModel>(cancellationToken) 
                ?? throw new InvalidOperationException("The server returned an empty agent.");
        }

        public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
        {
            using var response = await _httpClient.DeleteAsync($"/api/agents/{id}", cancellationToken);

            response.EnsureSuccessStatusCode();
        }
    }
}
