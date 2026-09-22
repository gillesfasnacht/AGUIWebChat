using AGUIWebChat.Contracts.AI;
using AGUIWebChat.Contracts.AI.Models;
using AGUIWebChat.Server.Mapping;
using AGUIWebChat.Server.Services.AI;
using AGUIWebChat.Tests.Infrastructure;
using Mapster;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AGUIWebChat.Tests.Services.AI
{
    public sealed class AIModelServiceTests
    {
        public AIModelServiceTests()
        {
            MapsterExtensions.RegisterMappings();
        }

        [Fact]
        public async Task CreateAsync_ShouldCreateModelWithDefaultsAndReasoningEfforts()
        {
            var cancellationToken = TestContext.Current.CancellationToken;

            await using var database = await TestDbContextFactory.CreateAsync(cancellationToken);

            var service = new AIModelService(database.DbContext);

            var model = CreateGraniteModel();

            var result = await service.CreateAsync(
                model,
                TestContext.Current.CancellationToken);

            Assert.NotEqual(0, result.Id);

            Assert.Equal(
                "granite4.2:8b",
                result.ModelId);

            Assert.Equal(
                "Granite 4.2 8B",
                result.DisplayName);

            Assert.Equal(
                ThinkingMode.Effort,
                result.ThinkingMode);

            Assert.Equal(0.7, result.Temperature);
            Assert.Equal(0.9, result.TopP);
            Assert.Equal(40, result.TopK);
            Assert.Equal(8192, result.NumCtx);

            Assert.Equal(
                3,
                result.ReasoningEfforts.Count);

            Assert.Single(
                result.ReasoningEfforts,
                x => x.IsDefault);
        }

        [Fact]
        public async Task UpdateAsync_ShouldUpdateDefaultsAndReplaceReasoningEfforts()
        {
            var cancellationToken = TestContext.Current.CancellationToken;

            await using var database = await TestDbContextFactory.CreateAsync(cancellationToken);

            var service =
                new AIModelService(database.DbContext);

            // Arrange : création du modèle initial
            var model = CreateGraniteModel();

            var created = await service.CreateAsync(model, cancellationToken);

            // Modification des propriétés simples
            created.DisplayName = "Granite 4.2 8B - Updated";
            created.ContextWindow = 65536;

            // Modification des defaults
            created.Temperature = 0.5;
            created.TopP = 0.95;
            created.TopK = 50;
            created.NumCtx = 16384;

            // Remplacement complet des efforts
            created.ReasoningEfforts =
            [
                new ReasoningEffortEditModel
                {
                    DisplayName = "Minimal",
                    Value = "minimal",
                    SortOrder = 1,
                    IsDefault = false
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
                    SortOrder = 3,
                    IsDefault = false
                }
            ];

            // Act
            var updated = await service.UpdateAsync(created, cancellationToken);

            // Assert : résultat retourné par le service
            Assert.Equal("Granite 4.2 8B - Updated", updated.DisplayName);

            Assert.Equal(65536, updated.ContextWindow);

            Assert.Equal(0.5, updated.Temperature);

            Assert.Equal(0.95, updated.TopP);

            Assert.Equal(50, updated.TopK);

            Assert.Equal(16384, updated.NumCtx);

            Assert.Equal(3, updated.ReasoningEfforts.Count);

            Assert.Contains(
                updated.ReasoningEfforts,
                x => x.Value == "minimal");

            Assert.Contains(
                updated.ReasoningEfforts,
                x => x.Value == "standard");

            Assert.Contains(
                updated.ReasoningEfforts,
                x => x.Value == "extended");

            Assert.DoesNotContain(
                updated.ReasoningEfforts,
                x => x.Value == "low");

            Assert.DoesNotContain(
                updated.ReasoningEfforts,
                x => x.Value == "medium");

            Assert.DoesNotContain(
                updated.ReasoningEfforts,
                x => x.Value == "high");

            Assert.Single(
                updated.ReasoningEfforts,
                x => x.IsDefault);

            database.DbContext.ChangeTracker.Clear();

            var entity = await database.DbContext.AIModels
                .AsNoTracking()
                .Include(x => x.Defaults)
                .Include(x => x.ReasoningEfforts)
                .SingleAsync(
                    x => x.Id == updated.Id,
                    cancellationToken);

            Assert.NotNull(entity.Defaults);

            Assert.Equal(0.5, entity.Defaults.Temperature);

            Assert.Equal(0.95, entity.Defaults.TopP);

            Assert.Equal(50, entity.Defaults.TopK);

            Assert.Equal(16384, entity.Defaults.NumCtx);

            Assert.Equal(3, entity.ReasoningEfforts.Count);

            Assert.DoesNotContain(
                entity.ReasoningEfforts,
                x => x.Value == "low");

            Assert.DoesNotContain(
                entity.ReasoningEfforts,
                x => x.Value == "medium");

            Assert.DoesNotContain(
                entity.ReasoningEfforts,
                x => x.Value == "high");

            Assert.Contains(
                entity.ReasoningEfforts,
                x => x.Value == "minimal");

            Assert.Contains(
                entity.ReasoningEfforts,
                x => x.Value == "standard");

            Assert.Contains(
                entity.ReasoningEfforts,
                x => x.Value == "extended");
        }

        [Fact]
        public async Task UpdateAsync_WhenThinkingModeChangesFromEffortToOnOff_ShouldRemoveReasoningEfforts()
        {
            var cancellationToken =  TestContext.Current.CancellationToken;

            await using var database = await TestDbContextFactory.CreateAsync(cancellationToken);

            var service = new AIModelService(database.DbContext);

            // Arrange
            var model = CreateGraniteModel();

            var created = await service.CreateAsync(
                model,
                cancellationToken);

            Assert.Equal(3, created.ReasoningEfforts.Count);

            // Act
            created.ThinkingMode = ThinkingMode.OnOff;

            var updated = await service.UpdateAsync(
                created,
                cancellationToken);

            // Assert sur le résultat du service
            Assert.Equal(
                ThinkingMode.OnOff,
                updated.ThinkingMode);

            Assert.Empty(updated.ReasoningEfforts);

            // On vide le tracking pour forcer une vraie relecture SQLite
            database.DbContext.ChangeTracker.Clear();

            var entity = await database.DbContext.AIModels
                .AsNoTracking()
                .Include(x => x.ReasoningEfforts)
                .SingleAsync(
                    x => x.Id == updated.Id,
                    cancellationToken);

            Assert.Equal(ThinkingMode.OnOff, entity.ThinkingMode);

            Assert.Empty(entity.ReasoningEfforts);
        }

        [Fact]
        public async Task UpdateAsync_WhenAllDefaultsAreNull_ShouldRemoveDefaults()
        {
            var cancellationToken = TestContext.Current.CancellationToken;

            await using var database = await TestDbContextFactory.CreateAsync(cancellationToken);

            var service = new AIModelService(database.DbContext);

            // Arrange
            var model = CreateGraniteModel();

            var created = await service.CreateAsync(model, cancellationToken);

            Assert.NotNull(created.Temperature);
            Assert.NotNull(created.TopP);
            Assert.NotNull(created.TopK);
            Assert.NotNull(created.NumCtx);

            // Act
            created.Temperature = null;
            created.TopP = null;
            created.TopK = null;
            created.NumCtx = null;

            var updated = await service.UpdateAsync(created, cancellationToken);

            // Assert sur le résultat retourné
            Assert.Null(updated.Temperature);
            Assert.Null(updated.TopP);
            Assert.Null(updated.TopK);
            Assert.Null(updated.NumCtx);

            // Relecture réelle depuis SQLite
            database.DbContext.ChangeTracker.Clear();

            var entity = await database.DbContext.AIModels
                .AsNoTracking()
                .Include(x => x.Defaults)
                .SingleAsync(
                    x => x.Id == updated.Id,
                    cancellationToken);

            Assert.Null(entity.Defaults);

            var defaultsExist =
                await database.DbContext.AIModelDefaults
                    .AsNoTracking()
                    .AnyAsync(
                        x => x.AIModelId == updated.Id,
                        cancellationToken);

            Assert.False(defaultsExist);
        }

        [Fact]
        public async Task DeleteAsync_ShouldDeleteModelAndRelatedData()
        {
            var cancellationToken = TestContext.Current.CancellationToken;

            await using var database = await TestDbContextFactory.CreateAsync(cancellationToken);

            var service = new AIModelService(database.DbContext);

            // Arrange
            var model = CreateGraniteModel();

            var created = await service.CreateAsync(model, cancellationToken);

            // Act
            await service.DeleteAsync(created.Id, cancellationToken);

            // Force une nouvelle lecture de la base
            database.DbContext.ChangeTracker.Clear();

            // Assert
            var modelExists =
                await database.DbContext.AIModels
                    .AsNoTracking()
                    .AnyAsync(
                        x => x.Id == created.Id,
                        cancellationToken);

            var defaultsExist =
                await database.DbContext.AIModelDefaults
                    .AsNoTracking()
                    .AnyAsync(
                        x => x.AIModelId == created.Id,
                        cancellationToken);

            var effortsExist =
                await database.DbContext.AIModelReasoningEfforts
                    .AsNoTracking()
                    .AnyAsync(
                        x => x.AIModelId == created.Id,
                        cancellationToken);

            Assert.False(modelExists);
            Assert.False(defaultsExist);
            Assert.False(effortsExist);
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
    }
}
