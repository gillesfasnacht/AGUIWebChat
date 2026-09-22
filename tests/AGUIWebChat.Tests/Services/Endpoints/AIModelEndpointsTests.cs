using AGUIWebChat.Contracts.AI;
using AGUIWebChat.Contracts.AI.Models;
using AGUIWebChat.Tests.Infrastructure;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace AGUIWebChat.Tests.Services.Endpoints
{
    public sealed class AIModelEndpointsTests
    {
        [Fact]
        public async Task GetModels_ShouldReturnOk()
        {
            var cancellationToken = TestContext.Current.CancellationToken;

            await using var factory = new AGUIWebChatWebApplicationFactory();

            using var client = factory.CreateClient();

            var response = await client.GetAsync(
                "/api/models",
                cancellationToken);

            Assert.Equal(
                HttpStatusCode.OK,
                response.StatusCode);
        }

        [Fact]
        public async Task CreateModel_ShouldReturnCreatedModel()
        {
            var cancellationToken = TestContext.Current.CancellationToken;

            await using var factory = new AGUIWebChatWebApplicationFactory();

            using var client = factory.CreateClient();

            var model = CreateGraniteModel();

            var response = await client.PostAsJsonAsync(
                "/api/models",
                model,
                cancellationToken);

            Assert.Equal(
                HttpStatusCode.Created,
                response.StatusCode);

            var created = await response.Content.ReadFromJsonAsync<AIModelEditModel>(
                JsonOptions,
                cancellationToken);

            Assert.NotNull(created);

            Assert.True(created.Id > 0);

            Assert.Equal(
                "granite4.2:8b",
                created.ModelId);

            Assert.Equal(
                ThinkingMode.Effort,
                created.ThinkingMode);

            Assert.Equal(3, created.ReasoningEfforts.Count);

            Assert.Equal(0.7, created.Temperature);

            Assert.NotNull(response.Headers.Location);

            Assert.Equal(
                $"/api/models/{created.Id}",
                response.Headers.Location.ToString());
        }

        private static AIModelEditModel CreateGraniteModel()
        {
            return new AIModelEditModel
            {
                ProviderId = 1,

                ModelId = "granite4.2:8b",
                DisplayName = "Granite 4.2 8B",

                ThinkingMode = ThinkingMode.Effort,

                SupportsVision = false,
                SupportsTools = true,
                SupportsStreaming = true,

                ContextWindow = 131072,

                Temperature = 0.7,
                TopP = 0.9,
                TopK = 40,
                NumCtx = 8192,

                IsEnabled = true,

                ReasoningEfforts =
                [
                    new ReasoningEffortEditModel
                {
                    DisplayName = "Low",
                    Value = "low",
                    SortOrder = 1
                },
                new ReasoningEffortEditModel
                {
                    DisplayName = "Medium",
                    Value = "medium",
                    SortOrder = 2,
                    IsDefault = true
                },
                new ReasoningEffortEditModel
                {
                    DisplayName = "High",
                    Value = "high",
                    SortOrder = 3
                }
                ]
            };
        }

        [Fact]
        public async Task ModelCrud_ShouldCreateReadUpdateDeleteModel()
        {
            var cancellationToken = TestContext.Current.CancellationToken;

            await using var factory = new AGUIWebChatWebApplicationFactory();

            using var client = factory.CreateClient();

            // ---------------------------------------------------------
            // CREATE
            // ---------------------------------------------------------

            var model = CreateGraniteModel();

            var createResponse = await client.PostAsJsonAsync(
                "/api/models",
                model,
                cancellationToken);

            Assert.Equal(
                HttpStatusCode.Created,
                createResponse.StatusCode);

            var created =
                await createResponse.Content
                    .ReadFromJsonAsync<AIModelEditModel>(
                        JsonOptions,
                        cancellationToken);

            Assert.NotNull(created);
            Assert.True(created.Id > 0);

            // ---------------------------------------------------------
            // READ
            // ---------------------------------------------------------

            var getResponse = await client.GetAsync(
                $"/api/models/{created.Id}",
                cancellationToken);

            Assert.Equal(
                HttpStatusCode.OK,
                getResponse.StatusCode);

            var loaded =
                await getResponse.Content
                    .ReadFromJsonAsync<AIModelEditModel>(
                        JsonOptions,
                        cancellationToken);

            Assert.NotNull(loaded);

            Assert.Equal(
                "granite4.2:8b",
                loaded.ModelId);

            Assert.Equal(
                ThinkingMode.Effort,
                loaded.ThinkingMode);

            // ---------------------------------------------------------
            // UPDATE
            // ---------------------------------------------------------

            loaded.DisplayName = "Granite 4.2 8B - Updated";

            loaded.Temperature = 0.5;
            loaded.NumCtx = 16384;

            loaded.ReasoningEfforts =
            [
                new ReasoningEffortEditModel
                {
                    DisplayName = "Minimal",
                    Value = "minimal",
                    SortOrder = 1
                },
                new ReasoningEffortEditModel
                {
                    DisplayName = "Standard",
                    Value = "standard",
                    SortOrder = 2,
                    IsDefault = true
                },
                new ReasoningEffortEditModel
                {
                    DisplayName = "Extended",
                    Value = "extended",
                    SortOrder = 3
                }
            ];

            var updateResponse = await client.PutAsJsonAsync(
                $"/api/models/{loaded.Id}",
                loaded,
                cancellationToken);

            Assert.Equal(
                HttpStatusCode.OK,
                updateResponse.StatusCode);

            var updated =
                await updateResponse.Content
                    .ReadFromJsonAsync<AIModelEditModel>(
                        JsonOptions,
                        cancellationToken);

            Assert.NotNull(updated);

            Assert.Equal(
                "Granite 4.2 8B - Updated",
                updated.DisplayName);

            Assert.Equal(0.5, updated.Temperature);

            Assert.Equal(16384, updated.NumCtx);

            Assert.Contains(
                updated.ReasoningEfforts,
                x => x.Value == "standard");

            Assert.DoesNotContain(
                updated.ReasoningEfforts,
                x => x.Value == "medium");

            // ---------------------------------------------------------
            // DELETE
            // ---------------------------------------------------------

            var deleteResponse = await client.DeleteAsync(
                $"/api/models/{updated.Id}",
                cancellationToken);

            Assert.Equal(
                HttpStatusCode.NoContent,
                deleteResponse.StatusCode);

            // ---------------------------------------------------------
            // VERIFY DELETE
            // ---------------------------------------------------------

            var afterDeleteResponse = await client.GetAsync(
                $"/api/models/{updated.Id}",
                cancellationToken);

            Assert.Equal(
                HttpStatusCode.NotFound,
                afterDeleteResponse.StatusCode);
        }

        private static readonly JsonSerializerOptions JsonOptions =
            new(JsonSerializerDefaults.Web)
            {
                Converters =
                {
                    new JsonStringEnumConverter()
                }
            };
    }
}
