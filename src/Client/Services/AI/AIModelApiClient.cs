using AGUIWebChat.Contracts.AI.Models;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AGUIWebChat.Client.Services.AI
{
    public sealed class AIModelApiClient
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            Converters = { new JsonStringEnumConverter() }
        };

        private readonly HttpClient _httpClient;

        public AIModelApiClient(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<IReadOnlyList<AIModelEditModel>> GetModelsAsync(bool enabledOnly = false, CancellationToken cancellationToken = default)
        {
            var models =
                await _httpClient.GetFromJsonAsync<List<AIModelEditModel>>(
                    $"/api/models?enabledOnly={enabledOnly.ToString().ToLowerInvariant()}",
                    JsonOptions,
                    cancellationToken);

            return models ?? [];
        }

        public async Task<AIModelEditModel?> GetModelAsync(int id, CancellationToken cancellationToken = default)
        {
            using var response = await _httpClient.GetAsync($"/api/models/{id}", cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<AIModelEditModel>(JsonOptions, cancellationToken);
        }

        public async Task<AIModelEditModel> CreateAsync(AIModelEditModel model, CancellationToken cancellationToken = default)
        {
            using var response = await _httpClient.PostAsJsonAsync("/api/models", model, JsonOptions, cancellationToken);

            response.EnsureSuccessStatusCode();

            return await ReadModelAsync(response, cancellationToken);
        }

        public async Task<AIModelEditModel> UpdateAsync(AIModelEditModel model, CancellationToken cancellationToken = default)
        {
            using var response = await _httpClient.PutAsJsonAsync($"/api/models/{model.Id}", model, JsonOptions, cancellationToken);

            response.EnsureSuccessStatusCode();

            return await ReadModelAsync(response, cancellationToken);
        }

        public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
        {
            using var response = await _httpClient.DeleteAsync($"/api/models/{id}", cancellationToken);

            response.EnsureSuccessStatusCode();
        }

        private static async Task<AIModelEditModel> ReadModelAsync(HttpResponseMessage response, CancellationToken cancellationToken)
        {
            var model = await response.Content.ReadFromJsonAsync<AIModelEditModel>(JsonOptions, cancellationToken);

            return model ?? throw new InvalidOperationException("The API returned an empty AI model response.");
        }
    }
}
