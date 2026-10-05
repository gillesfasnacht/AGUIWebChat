using AGUIWebChat.Contracts.AI;
using AGUIWebChat.Contracts.AI.Agents;
using AGUIWebChat.Server.Data;
using AGUIWebChat.Server.Domain.AI;
using AGUIWebChat.Server.Services.AI;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace AGUIWebChat.Server.Tests.Services.AI
{
    public sealed class AIAgentServiceTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ChatDbContext _dbContext;
        private readonly AIAgentService _service;

        public AIAgentServiceTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");

            _connection.Open();

            var options = new DbContextOptionsBuilder<ChatDbContext>().UseSqlite(_connection).Options;

            _dbContext = new ChatDbContext(options);

            _dbContext.Database.EnsureCreated();

            _service = new AIAgentService(_dbContext);
        }

        [Fact]
        public async Task CreateAsync_ShouldCreateAgentWithOverrides()
        {
            var cancellationToken = TestContext.Current.CancellationToken;

            var model = await CreateModelAsync(
                "granite-test",
                ThinkingMode.Effort,
                cancellationToken);

            model.ReasoningEfforts.Add(new AIModelReasoningEffort
            {
                DisplayName = "Low",
                Value = "low",
                SortOrder = 10
            });

            model.ReasoningEfforts.Add(new AIModelReasoningEffort
            {
                DisplayName = "High",
                Value = "high",
                SortOrder = 20,
                IsDefault = true
            });

            await _dbContext.SaveChangesAsync(cancellationToken);

            var request = new AIAgentEditModel
            {
                Name = "InvoiceValidation",
                Description = "Validates invoices.",
                SystemPrompt = "Validate the supplied invoice.",
                AIModelId = model.Id,

                Temperature = 0.2,
                TopP = 0.8,
                TopK = 20,
                NumCtx = 4096,

                ReasoningEffort = "high",
                IsEnabled = true
            };

            var result = await _service.CreateAsync(request, cancellationToken);

            Assert.True(result.Id > 0);
            Assert.Equal("InvoiceValidation", result.Name);
            Assert.Equal(model.Id, result.AIModelId);
            Assert.Equal(model.DisplayName, result.AIModelDisplayName);
            Assert.Equal(0.2, result.Temperature);
            Assert.Equal(0.8, result.TopP);
            Assert.Equal(20, result.TopK);
            Assert.Equal(4096, result.NumCtx);

            Assert.Equal("high", result.ReasoningEffort);

            var entity = await _dbContext.AIAgents.SingleAsync(x => x.Id == result.Id, cancellationToken);

            Assert.Equal("InvoiceValidation", entity.Name);
        }

        [Fact]
        public async Task CreateAsync_ShouldAllowNullOverrides()
        {
            var cancellationToken = TestContext.Current.CancellationToken;

            var model = await CreateModelAsync("granite-defaults", ThinkingMode.None, cancellationToken);

            var request = new AIAgentEditModel
            {
                Name = "DefaultAgent",
                SystemPrompt = "You are an assistant.",
                AIModelId = model.Id,
                IsEnabled = true
            };

            var result = await _service.CreateAsync(request, cancellationToken);

            Assert.Null(result.Temperature);
            Assert.Null(result.TopP);
            Assert.Null(result.TopK);
            Assert.Null(result.NumCtx);
            Assert.Null(result.ReasoningEffort);
        }

        [Fact]
        public async Task UpdateAsync_ShouldUpdateAgentAndChangeModel()
        {
            var cancellationToken = TestContext.Current.CancellationToken;

            var firstModel = await CreateModelAsync("model-1", ThinkingMode.None, cancellationToken);

            var secondModel = await CreateModelAsync("model-2", ThinkingMode.Effort, cancellationToken);

            secondModel.ReasoningEfforts.Add(new AIModelReasoningEffort
            {
                DisplayName = "Medium",
                Value = "medium",
                SortOrder = 10,
                IsDefault = true
            });

            await _dbContext.SaveChangesAsync(cancellationToken);

            var created = await _service.CreateAsync(new AIAgentEditModel
            {
                Name = "InvoiceAgent",
                SystemPrompt = "Initial prompt.",
                AIModelId = firstModel.Id
            },
            cancellationToken);

            created.Description = "Updated description";
            created.SystemPrompt = "Updated prompt.";
            created.AIModelId = secondModel.Id;
            created.Temperature = 0.3;
            created.ReasoningEffort = "medium";

            var updated = await _service.UpdateAsync(created, cancellationToken);

            Assert.Equal(secondModel.Id, updated.AIModelId);
            Assert.Equal(secondModel.DisplayName, updated.AIModelDisplayName);
            Assert.Equal("Updated description", updated.Description);
            Assert.Equal("Updated prompt.", updated.SystemPrompt);
            Assert.Equal(0.3, updated.Temperature);
            Assert.Equal("medium", updated.ReasoningEffort);
        }

        [Fact]
        public async Task DeleteAsync_ShouldDeleteAgent()
        {
            var cancellationToken = TestContext.Current.CancellationToken;

            var model = await CreateModelAsync("delete-model", ThinkingMode.None, cancellationToken);

            var created = await _service.CreateAsync(new AIAgentEditModel
            {
                Name = "AgentToDelete",
                SystemPrompt = "Temporary agent.",
                AIModelId = model.Id
            },
            cancellationToken);

            await _service.DeleteAsync(created.Id, cancellationToken);

            var exists = await _dbContext.AIAgents.AnyAsync(x => x.Id == created.Id, cancellationToken);

            Assert.False(exists);
        }

        [Fact]
        public async Task CreateAsync_WhenModelIsDisabled_ShouldFail()
        {
            var cancellationToken = TestContext.Current.CancellationToken;

            var model = await CreateModelAsync("disabled-model", ThinkingMode.None, cancellationToken, isEnabled: false);

            var request = new AIAgentEditModel
            {
                Name = "InvalidAgent",
                SystemPrompt = "Test.",
                AIModelId = model.Id
            };

            await Assert.ThrowsAsync<ModelValidationException>(() => _service.CreateAsync(request, cancellationToken));
        }

        [Fact]
        public async Task CreateAsync_WhenReasoningEffortIsInvalid_ShouldFail()
        {
            var cancellationToken = TestContext.Current.CancellationToken;

            var model = await CreateModelAsync("reasoning-model", ThinkingMode.Effort, cancellationToken);

            model.ReasoningEfforts.Add(new AIModelReasoningEffort
            {
                DisplayName = "Low",
                Value = "low",
                SortOrder = 10,
                IsDefault = true
            });

            await _dbContext.SaveChangesAsync(cancellationToken);

            var request = new AIAgentEditModel
            {
                Name = "InvalidReasoningAgent",
                SystemPrompt = "Test.",
                AIModelId = model.Id,

                // Le modèle ne propose que "low".
                ReasoningEffort = "ultra"
            };

            await Assert.ThrowsAsync<ModelValidationException>(() => _service.CreateAsync(request, cancellationToken));
        }

        [Fact]
        public async Task CreateAsync_WhenNameAlreadyExists_ShouldFail()
        {
            var cancellationToken = TestContext.Current.CancellationToken;

            var model = await CreateModelAsync("unique-name-model", ThinkingMode.None, cancellationToken);

            await _service.CreateAsync(new AIAgentEditModel
            {
                Name = "InvoiceAgent",
                SystemPrompt = "First agent.",
                AIModelId = model.Id
            },
            cancellationToken);

            var duplicate = new AIAgentEditModel
            {
                Name = "InvoiceAgent",
                SystemPrompt = "Second agent.",
                AIModelId = model.Id
            };

            await Assert.ThrowsAsync<ModelValidationException>(() => _service.CreateAsync(duplicate, cancellationToken));
        }

        [Fact]
        public async Task CreateAsync_WhenNameIsEmpty_ShouldFail()
        {
            var cancellationToken = TestContext.Current.CancellationToken;

            var model = await CreateModelAsync("empty-name-model", ThinkingMode.None, cancellationToken);

            var request = new AIAgentEditModel
            {
                Name = "   ",
                SystemPrompt = "You are an assistant.",
                AIModelId = model.Id
            };

            await Assert.ThrowsAsync<ModelValidationException>(() => _service.CreateAsync(request, cancellationToken));
        }

        [Fact]
        public async Task CreateAsync_WhenSystemPromptIsEmpty_ShouldFail()
        {
            var cancellationToken = TestContext.Current.CancellationToken;

            var model = await CreateModelAsync("empty-prompt-model", ThinkingMode.None, cancellationToken);

            var request = new AIAgentEditModel
            {
                Name = "AgentWithoutPrompt",
                SystemPrompt = "   ",
                AIModelId = model.Id
            };

            await Assert.ThrowsAsync<ModelValidationException>(() => _service.CreateAsync(request, cancellationToken));
        }

        [Fact]
        public async Task CreateAsync_WhenModelDoesNotExist_ShouldFail()
        {
            var cancellationToken = TestContext.Current.CancellationToken;

            var request = new AIAgentEditModel
            {
                Name = "UnknownModelAgent",
                SystemPrompt = "You are an assistant.",

                // ID valide syntaxiquement,
                // mais inexistant en base.
                AIModelId = 999999
            };

            await Assert.ThrowsAsync<ModelValidationException>(() => _service.CreateAsync(request, cancellationToken));
        }

        [Fact]
        public async Task CreateAsync_WhenReasoningEffortIsSetOnNonEffortModel_ShouldFail()
        {
            var cancellationToken = TestContext.Current.CancellationToken;

            var model = await CreateModelAsync("non-effort-model", ThinkingMode.OnOff, cancellationToken);

            var request = new AIAgentEditModel
            {
                Name = "InvalidEffortAgent",
                SystemPrompt = "You are an assistant.",
                AIModelId = model.Id,

                ReasoningEffort = "high"
            };

            await Assert.ThrowsAsync<ModelValidationException>(() => _service.CreateAsync(request, cancellationToken));
        }

        [Fact]
        public async Task UpdateAsync_WhenNameIsUnchanged_ShouldSucceed()
        {
            var cancellationToken = TestContext.Current.CancellationToken;

            var model = await CreateModelAsync(
                "update-same-name-model",
                ThinkingMode.None,
                cancellationToken);

            var created = await _service.CreateAsync(new AIAgentEditModel
            {
                Name = "InvoiceAgent",
                Description = "Initial description",
                SystemPrompt = "Initial prompt.",
                AIModelId = model.Id
            },
            cancellationToken);

            created.Description = "Updated description";
            created.SystemPrompt = "Updated prompt.";

            var updated = await _service.UpdateAsync(created, cancellationToken);

            Assert.Equal(created.Id, updated.Id);
            Assert.Equal("InvoiceAgent", updated.Name);
            Assert.Equal("Updated description", updated.Description);
            Assert.Equal("Updated prompt.", updated.SystemPrompt);
        }

        private async Task<AIModel> CreateModelAsync(
            string modelId,
            ThinkingMode thinkingMode,
            CancellationToken cancellationToken,
            bool isEnabled = true)
        {
            var provider = await GetOrCreateProviderAsync(cancellationToken);

            var model = new AIModel
            {
                ProviderId = provider.Id,
                Provider = provider,

                ModelId = modelId,
                DisplayName = modelId,

                ThinkingMode = thinkingMode,

                SupportsVision = false,
                SupportsTools = true,
                SupportsStreaming = true,

                IsEnabled = isEnabled
            };

            _dbContext.AIModels.Add(model);

            await _dbContext.SaveChangesAsync(cancellationToken);

            return model;
        }

        private async Task<AIProvider> GetOrCreateProviderAsync(CancellationToken cancellationToken)
        {
            var provider = await _dbContext.AIProviders.FirstOrDefaultAsync(cancellationToken);

            if (provider is not null) return provider;

            provider = new AIProvider
            {
                Name = "Test Provider",
                ProviderType = "Test",
                IsEnabled = true
            };

            _dbContext.AIProviders.Add(provider);

            await _dbContext.SaveChangesAsync(cancellationToken);

            return provider;
        }

        public void Dispose()
        {
            _dbContext.Dispose();
            _connection.Dispose();
        }
    }
}
