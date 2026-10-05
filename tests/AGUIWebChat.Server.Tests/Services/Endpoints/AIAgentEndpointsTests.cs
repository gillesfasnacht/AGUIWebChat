using AGUIWebChat.Contracts.AI;
using AGUIWebChat.Contracts.AI.Agents;
using AGUIWebChat.Contracts.AI.Models;
using AGUIWebChat.Server.Tests.Infrastructure;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace AGUIWebChat.Server.Tests.Services.Endpoints
{
    public sealed class AIAgentEndpointsTests
    {
        [Fact]
        public async Task GetAgents_ShouldReturnOk()
        {
            var cancellationToken = TestContext.Current.CancellationToken;

            await using var factory = new AGUIWebChatWebApplicationFactory();

            using var client = factory.CreateClient();

            var response = await client.GetAsync("/api/agents", cancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task AgentCrud_ShouldCreateReadUpdateDeleteAgent()
        {
            var cancellationToken = TestContext.Current.CancellationToken;

            await using var factory = new AGUIWebChatWebApplicationFactory();

            using var client = factory.CreateClient();

            // ---------------------------------------------------------
            // CREATE MODEL
            // ---------------------------------------------------------

            var model = CreateGraniteModel();

            var modelResponse = await client.PostAsJsonAsync("/api/models", model, cancellationToken);

            Assert.Equal(HttpStatusCode.Created, modelResponse.StatusCode);

            var createdModel = await modelResponse.Content.ReadFromJsonAsync<AIModelEditModel>(JsonOptions, cancellationToken);

            Assert.NotNull(createdModel);
            Assert.True(createdModel.Id > 0);

            // ---------------------------------------------------------
            // CREATE AGENT
            // ---------------------------------------------------------

            var agent = new AIAgentEditModel
            {
                Name = "InvoiceAgent",

                Description = "Agent responsible for invoice validation.",

                SystemPrompt = "Validate the supplied invoice.",

                AIModelId = createdModel.Id,

                Temperature = 0.3,
                TopP = 0.8,
                TopK = 30,
                NumCtx = 4096,

                ReasoningEffort = "high",

                IsEnabled = true
            };

            var createResponse = await client.PostAsJsonAsync("/api/agents", agent, cancellationToken);

            Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

            var created = await createResponse.Content.ReadFromJsonAsync<AIAgentEditModel>(JsonOptions, cancellationToken);

            Assert.NotNull(created);
            Assert.True(created.Id > 0);
            Assert.Equal("InvoiceAgent", created.Name);
            Assert.Equal(createdModel.Id, created.AIModelId);
            Assert.Equal(createdModel.DisplayName, created.AIModelDisplayName);
            Assert.Equal("high", created.ReasoningEffort);
            Assert.NotNull(createResponse.Headers.Location);
            Assert.Equal($"/api/agents/{created.Id}", createResponse.Headers.Location.ToString());

            // ---------------------------------------------------------
            // READ
            // ---------------------------------------------------------

            var getResponse = await client.GetAsync($"/api/agents/{created.Id}", cancellationToken);

            Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

            var loaded = await getResponse.Content.ReadFromJsonAsync<AIAgentEditModel>(JsonOptions, cancellationToken);

            Assert.NotNull(loaded);
            Assert.Equal("InvoiceAgent", loaded.Name);
            Assert.Equal(createdModel.Id, loaded.AIModelId);
            Assert.Equal(createdModel.DisplayName, loaded.AIModelDisplayName);

            // ---------------------------------------------------------
            // UPDATE
            // ---------------------------------------------------------

            loaded.Description = "Updated invoice validation agent.";
            loaded.SystemPrompt = "Validate invoices using the updated rules.";
            loaded.Temperature = 0.5;
            loaded.NumCtx = 8192;
            loaded.ReasoningEffort = "medium";

            var updateResponse = await client.PutAsJsonAsync($"/api/agents/{loaded.Id}", loaded, cancellationToken);

            Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

            var updated = await updateResponse.Content.ReadFromJsonAsync<AIAgentEditModel>(JsonOptions, cancellationToken);

            Assert.NotNull(updated);
            Assert.Equal("Updated invoice validation agent.", updated.Description);
            Assert.Equal("Validate invoices using the updated rules.", updated.SystemPrompt);
            Assert.Equal(0.5, updated.Temperature);
            Assert.Equal(8192, updated.NumCtx);
            Assert.Equal("medium", updated.ReasoningEffort);

            // ---------------------------------------------------------
            // DELETE
            // ---------------------------------------------------------

            var deleteResponse = await client.DeleteAsync($"/api/agents/{updated.Id}", cancellationToken);

            Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

            // ---------------------------------------------------------
            // VERIFY DELETE
            // ---------------------------------------------------------

            var afterDeleteResponse = await client.GetAsync($"/api/agents/{updated.Id}", cancellationToken);

            Assert.Equal(HttpStatusCode.NotFound, afterDeleteResponse.StatusCode);
        }

        [Theory]
        [InlineData("missing-model", 400)]
        [InlineData("invalid-effort", 400)]
        [InlineData("mismatch", 400)]
        [InlineData("missing-update", 404)]
        [InlineData("missing-delete", 404)]
        [InlineData("missing-get", 404)]
        [InlineData("duplicate", 409)]
        [InlineData("invalid-temperature", 400)]
        public async Task Errors_ShouldReturnProblemDetails(string scenario, int expectedStatus)
        {
            var cancellationToken = TestContext.Current.CancellationToken;

            await using var factory = new AGUIWebChatWebApplicationFactory();

            using var client = factory.CreateClient();

            // Création du modèle nécessaire aux scénarios
            // qui ont besoin d'un modèle valide.
            var modelResponse = await client.PostAsJsonAsync("/api/models", CreateGraniteModel(), cancellationToken);

            Assert.Equal(HttpStatusCode.Created, modelResponse.StatusCode);

            var model = await modelResponse.Content.ReadFromJsonAsync<AIModelEditModel>(JsonOptions, cancellationToken);

            Assert.NotNull(model);

            var agent = new AIAgentEditModel
            {
                Name = "ErrorTestAgent",
                SystemPrompt = "Test agent.",
                AIModelId = model.Id,
                ReasoningEffort = "medium",
                IsEnabled = true
            };

            switch (scenario)
            {
                case "duplicate":
                    using (var first = await client.PostAsJsonAsync("/api/agents", agent, cancellationToken))
                        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
                    break;

                case "invalid-temperature":
                    agent.Temperature = 3;
                    break;

                case "missing-update":
                    agent.Id = 999;
                    break;

                case "missing-model":
                    agent.AIModelId = 999999;
                    break;

                case "invalid-effort":
                    agent.ReasoningEffort = "ultra";
                    break;

                case "mismatch":
                    agent.Id = 42;
                    break;
            }

            using var response = scenario switch
            {
                "missing-get" => await client.GetAsync("/api/agents/999", cancellationToken),
                "missing-delete" => await client.DeleteAsync("/api/agents/999", cancellationToken),
                "missing-update" => await client.PutAsJsonAsync("/api/agents/999", agent, cancellationToken),
                "mismatch" => await client.PutAsJsonAsync("/api/agents/999", agent, cancellationToken),
                _ => await client.PostAsJsonAsync("/api/agents", agent, cancellationToken)
            };

            Assert.Equal(expectedStatus, (int)response.StatusCode);
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

            var problem = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);

            Assert.Equal(expectedStatus, problem.GetProperty("status").GetInt32());
            Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("title").GetString()));
            var detail = problem.GetProperty("detail").GetString();
            Assert.False(string.IsNullOrWhiteSpace(detail));
            if (expectedStatus == 400)
                Assert.Contains(detail, problem.GetProperty("errors").GetProperty("model")
                    .EnumerateArray().Select(x => x.GetString()));
            if (scenario == "mismatch")
                Assert.Equal("The route id does not match the agent id.", detail);
            if (scenario == "invalid-temperature")
                Assert.Equal("Temperature must be between 0 and 2.", detail);
        }

        private static AIModelEditModel CreateGraniteModel()
        {
            return new AIModelEditModel
            {
                ProviderId = 1,

                ModelId = "granite-agent-test",
                DisplayName = "Granite Agent Test",

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

        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            Converters =
            {
                new JsonStringEnumConverter()
            }
        };
    }
}
