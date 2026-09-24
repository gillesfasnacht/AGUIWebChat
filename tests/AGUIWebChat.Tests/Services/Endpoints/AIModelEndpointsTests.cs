using AGUIWebChat.Contracts.AI;
using AGUIWebChat.Contracts.AI.Models;
using AGUIWebChat.Server.Services.AI;
using AGUIWebChat.Tests.Infrastructure;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Net;
using System.Net.Http.Json;
using System.Reflection;
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
            var response = await client.GetAsync("/api/models", cancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task CreateModel_ShouldReturnCreatedModel()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            await using var factory = new AGUIWebChatWebApplicationFactory();
            using var client = factory.CreateClient();
            var model = CreateGraniteModel();
            var response = await client.PostAsJsonAsync("/api/models", model, cancellationToken);

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);

            var created = await response.Content.ReadFromJsonAsync<AIModelEditModel>(JsonOptions, cancellationToken);

            Assert.NotNull(created);
            Assert.True(created.Id > 0);
            Assert.Equal("granite4.2:8b", created.ModelId);
            Assert.Equal("Ollama", created.ProviderName);
            Assert.Equal(ThinkingMode.Effort, created.ThinkingMode);
            Assert.Equal(3, created.ReasoningEfforts.Count);
            Assert.Equal(0.7, created.Temperature);
            Assert.NotNull(response.Headers.Location);
            Assert.Equal($"/api/models/{created.Id}", response.Headers.Location.ToString());
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
            var createResponse = await client.PostAsJsonAsync("/api/models", model, cancellationToken);

            Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

            var created = await createResponse.Content.ReadFromJsonAsync<AIModelEditModel>(JsonOptions, cancellationToken);

            Assert.NotNull(created);
            Assert.True(created.Id > 0);

            // ---------------------------------------------------------
            // READ
            // ---------------------------------------------------------

            var getResponse = await client.GetAsync($"/api/models/{created.Id}", cancellationToken);

            Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

            var loaded = await getResponse.Content.ReadFromJsonAsync<AIModelEditModel>(JsonOptions, cancellationToken);

            Assert.NotNull(loaded);
            Assert.Equal("granite4.2:8b", loaded.ModelId);
            Assert.Equal("Ollama", loaded.ProviderName);
            Assert.Equal(ThinkingMode.Effort, loaded.ThinkingMode);

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

            var updateResponse = await client.PutAsJsonAsync($"/api/models/{loaded.Id}", loaded, cancellationToken);

            Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

            var updated = await updateResponse.Content.ReadFromJsonAsync<AIModelEditModel>(JsonOptions, cancellationToken);

            Assert.NotNull(updated);
            Assert.Equal("Granite 4.2 8B - Updated", updated.DisplayName);
            Assert.Equal(0.5, updated.Temperature);
            Assert.Equal(16384, updated.NumCtx);
            Assert.Contains(updated.ReasoningEfforts, x => x.Value == "standard");
            Assert.DoesNotContain(updated.ReasoningEfforts, x => x.Value == "medium");

            // ---------------------------------------------------------
            // DELETE
            // ---------------------------------------------------------

            var deleteResponse = await client.DeleteAsync($"/api/models/{updated.Id}", cancellationToken);

            Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

            // ---------------------------------------------------------
            // VERIFY DELETE
            // ---------------------------------------------------------

            var afterDeleteResponse = await client.GetAsync($"/api/models/{updated.Id}", cancellationToken);

            Assert.Equal(HttpStatusCode.NotFound, afterDeleteResponse.StatusCode);
        }

        [Theory]
        [InlineData("invalid", 400)]
        [InlineData("provider", 400)]
        [InlineData("mismatch", 400)]
        [InlineData("missing-get", 404)]
        [InlineData("missing-update", 404)]
        [InlineData("duplicate", 409)]
        [InlineData("malformed", 400)]
        public async Task Errors_ShouldReturnProblemDetails(string scenario, int expectedStatus)
        {
            var token = TestContext.Current.CancellationToken;
            await using var factory = new AGUIWebChatWebApplicationFactory();
            using var client = factory.CreateClient();
            var model = CreateGraniteModel();

            if (scenario == "invalid") model.ModelId = "";
            if (scenario == "provider") model.ProviderId = 999;
            if (scenario == "missing-update") model.Id = 999;
            if (scenario == "duplicate")
            {
                using var first = await client.PostAsJsonAsync("/api/models", model, token);
                Assert.Equal(HttpStatusCode.Created, first.StatusCode);
            }

            using var response = scenario switch
            {
                "mismatch" or "missing-update" => await client.PutAsJsonAsync("/api/models/999", model, token),
                "missing-get" => await client.GetAsync("/api/models/999", token),
                "malformed" => await client.PostAsync("/api/models",
                    new StringContent("{", System.Text.Encoding.UTF8, "application/json"), token),
                _ => await client.PostAsJsonAsync("/api/models", model, token)
            };

            Assert.Equal(expectedStatus, (int)response.StatusCode);
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

            var problem = await response.Content.ReadFromJsonAsync<JsonElement>(token);

            Assert.Equal(expectedStatus, problem.GetProperty("status").GetInt32());
            Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("title").GetString()));

            if (scenario is "invalid" or "provider" or "mismatch")
                Assert.True(problem.TryGetProperty("errors", out _));
        }

        [Fact]
        public async Task TestHost_ShouldUseItsOwnOllamaConfiguration()
        {
            await using var factory = new AGUIWebChatWebApplicationFactory();
            using var client = factory.CreateClient();

            var configuration = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
                .GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>(factory.Services);

            Assert.Equal("http://127.0.0.1:1", configuration["OLLAMA_ENDPOINT"]);
            Assert.Equal("catalog-test-model", configuration["OLLAMA_MODEL"]);

            using var response = await client.GetAsync("/api/models", TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        public class FailingServiceProxy : DispatchProxy
        {
            protected override object? Invoke(MethodInfo? method, object?[]? args)
                => throw new InvalidOperationException("private database details");
        }

        [Fact]
        public async Task UnexpectedError_ShouldReturnNeutralProblemDetails()
        {
            await using var factory = new AGUIWebChatWebApplicationFactory();
            await using var failingHost = factory.WithWebHostBuilder(builder =>
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<IAIModelService>();
                    services.AddScoped(_ => DispatchProxy.Create<IAIModelService, FailingServiceProxy>());
                }));

            using var client = failingHost.CreateClient();
            using var response = await client.GetAsync("/api/models", TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

            var json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

            Assert.DoesNotContain("private database details", json);

            using var document = JsonDocument.Parse(json);

            Assert.Equal(500, document.RootElement.GetProperty("status").GetInt32());
            Assert.Equal("An unexpected error occurred.", document.RootElement.GetProperty("detail").GetString());
            Assert.False(string.IsNullOrWhiteSpace(document.RootElement.GetProperty("traceId").GetString()));
        }

        private static readonly JsonSerializerOptions JsonOptions =
            new(JsonSerializerDefaults.Web)
            {
                Converters = { new JsonStringEnumConverter() }
            };
    }
}
