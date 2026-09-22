using AGUIWebChat.Contracts.AI;
using AGUIWebChat.Contracts.AI.Models;
using AGUIWebChat.Server.Data;
using AGUIWebChat.Server.Domain.AI;
using Mapster;
using Microsoft.EntityFrameworkCore;

namespace AGUIWebChat.Server.Services.AI
{
    public sealed class AIModelService : IAIModelService
    {
        private readonly ChatDbContext _dbContext;

        public AIModelService(ChatDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<IReadOnlyList<AIModelEditModel>> GetModelsAsync(
            bool enabledOnly = false,
            CancellationToken cancellationToken = default)
        {
            var query = _dbContext.AIModels
                .AsNoTracking()
                .Include(x => x.Defaults)
                .Include(x => x.ReasoningEfforts)
                .AsQueryable();

            if (enabledOnly)
            {
                query = query.Where(x => x.IsEnabled);
            }

            var entities = await query
                .OrderBy(x => x.DisplayName)
                .ToListAsync(cancellationToken);

            return entities
                .Select(x => x.Adapt<AIModelEditModel>())
                .ToList();
        }

        public async Task<AIModelEditModel?> GetModelAsync(
            int id,
            CancellationToken cancellationToken = default)
        {
            var entity = await _dbContext.AIModels
                .AsNoTracking()
                .Include(x => x.Defaults)
                .Include(x => x.ReasoningEfforts)
                .FirstOrDefaultAsync(
                    x => x.Id == id,
                    cancellationToken);

            return entity?.Adapt<AIModelEditModel>();
        }

        public async Task<AIModelEditModel?> GetModelByModelIdAsync(
            int providerId,
            string modelId,
            CancellationToken cancellationToken = default)
        {
            var entity = await _dbContext.AIModels
                .AsNoTracking()
                .Include(x => x.Defaults)
                .Include(x => x.ReasoningEfforts)
                .FirstOrDefaultAsync(
                    x => x.ProviderId == providerId &&
                         x.ModelId == modelId,
                    cancellationToken);

            return entity?.Adapt<AIModelEditModel>();
        }

        public async Task<AIModelEditModel> CreateAsync(
            AIModelEditModel model,
            CancellationToken cancellationToken = default)
        {
            Validate(model);

            await ValidateProviderAsync(
                model.ProviderId,
                cancellationToken);

            await ValidateModelIdIsUniqueAsync(
                model.ProviderId,
                model.ModelId,
                null,
                cancellationToken);

            var entity = model.Adapt<AIModel>();

            UpdateDefaults(entity, model);
            UpdateReasoningEfforts(entity, model);

            _dbContext.AIModels.Add(entity);

            await _dbContext.SaveChangesAsync(cancellationToken);

            return entity.Adapt<AIModelEditModel>();
        }

        public async Task<AIModelEditModel> UpdateAsync(
            AIModelEditModel model,
            CancellationToken cancellationToken = default)
        {
            Validate(model);

            await ValidateProviderAsync(
                model.ProviderId,
                cancellationToken);

            var entity = await _dbContext.AIModels
                .Include(x => x.Defaults)
                .Include(x => x.ReasoningEfforts)
                .FirstOrDefaultAsync(
                    x => x.Id == model.Id,
                    cancellationToken)
                ?? throw new InvalidOperationException(
                    $"AI model with id {model.Id} was not found.");

            await ValidateModelIdIsUniqueAsync(
                model.ProviderId,
                model.ModelId,
                model.Id,
                cancellationToken);

            // Mapster ne modifie ici que les propriétés scalaires.
            model.Adapt(entity);

            UpdateDefaults(entity, model);
            UpdateReasoningEfforts(entity, model);

            await _dbContext.SaveChangesAsync(cancellationToken);

            return entity.Adapt<AIModelEditModel>();
        }

        public async Task DeleteAsync(
            int id,
            CancellationToken cancellationToken = default)
        {
            var entity = await _dbContext.AIModels
                .FirstOrDefaultAsync(
                    x => x.Id == id,
                    cancellationToken);

            if (entity is null)
            {
                return;
            }

            _dbContext.AIModels.Remove(entity);

            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        private async Task ValidateProviderAsync(
            int providerId,
            CancellationToken cancellationToken)
        {
            var exists = await _dbContext.AIProviders
                .AnyAsync(
                    x => x.Id == providerId && x.IsEnabled,
                    cancellationToken);

            if (!exists)
            {
                throw new InvalidOperationException(
                    $"AI provider with id {providerId} does not exist or is disabled.");
            }
        }

        private async Task ValidateModelIdIsUniqueAsync(
            int providerId,
            string modelId,
            int? currentModelId,
            CancellationToken cancellationToken)
        {
            var exists = await _dbContext.AIModels
                .AnyAsync(
                    x => x.ProviderId == providerId &&
                         x.ModelId == modelId &&
                         (!currentModelId.HasValue ||
                          x.Id != currentModelId.Value),
                    cancellationToken);

            if (exists)
            {
                throw new InvalidOperationException(
                    $"Model '{modelId}' already exists for provider {providerId}.");
            }
        }

        private static void UpdateDefaults(
            AIModel entity,
            AIModelEditModel model)
        {
            if (!HasDefaults(model))
            {
                entity.Defaults = null;
                return;
            }

            entity.Defaults ??= new AIModelDefaults();

            entity.Defaults.Temperature = model.Temperature;
            entity.Defaults.TopP = model.TopP;
            entity.Defaults.TopK = model.TopK;
            entity.Defaults.NumCtx = model.NumCtx;
        }

        private static bool HasDefaults(AIModelEditModel model)
        {
            return model.Temperature.HasValue ||
                   model.TopP.HasValue ||
                   model.TopK.HasValue ||
                   model.NumCtx.HasValue;
        }

        private static void UpdateReasoningEfforts(
            AIModel entity,
            AIModelEditModel model)
        {
            entity.ReasoningEfforts.Clear();

            if (model.ThinkingMode != ThinkingMode.Effort)
            {
                return;
            }

            foreach (var effort in model.ReasoningEfforts
                         .OrderBy(x => x.SortOrder))
            {
                entity.ReasoningEfforts.Add(
                    new AIModelReasoningEffort
                    {
                        DisplayName = effort.DisplayName.Trim(),
                        Value = effort.Value.Trim(),
                        SortOrder = effort.SortOrder,
                        IsDefault = effort.IsDefault
                    });
            }
        }

        private static void Validate(AIModelEditModel model)
        {
            if (model.ProviderId <= 0)
            {
                throw new ArgumentException("A provider must be selected.");
            }

            if (string.IsNullOrWhiteSpace(model.ModelId))
            {
                throw new ArgumentException("ModelId is required.");
            }

            if (string.IsNullOrWhiteSpace(model.DisplayName))
            {
                throw new ArgumentException("DisplayName is required.");
            }

            if (model.ContextWindow is <= 0)
            {
                throw new ArgumentException("ContextWindow must be greater than zero.");
            }

            if (model.MaxOutputTokens is <= 0)
            {
                throw new ArgumentException("MaxOutputTokens must be greater than zero.");
            }

            if (model.Temperature is < 0)
            {
                throw new ArgumentException("Temperature cannot be negative.");
            }

            if (model.TopP is < 0 or > 1)
            {
                throw new ArgumentException("TopP must be between 0 and 1.");
            }

            if (model.TopK is <= 0)
            {
                throw new ArgumentException("TopK must be greater than zero.");
            }

            if (model.NumCtx is <= 0)
            {
                throw new ArgumentException("NumCtx must be greater than zero.");
            }

            ValidateReasoning(model);
        }

        private static void ValidateReasoning(
            AIModelEditModel model)
        {
            if (model.ThinkingMode != ThinkingMode.Effort)
            {
                return;
            }

            if (model.ReasoningEfforts.Count == 0)
            {
                throw new ArgumentException("At least one reasoning effort is required.");
            }

            if (model.ReasoningEfforts.Count(x => x.IsDefault) != 1)
            {
                throw new ArgumentException("Exactly one reasoning effort must be the default.");
            }

            if (model.ReasoningEfforts.Any(x => string.IsNullOrWhiteSpace(x.DisplayName)))
            {
                throw new ArgumentException("Every reasoning effort must have a display name.");
            }

            if (model.ReasoningEfforts.Any(x => string.IsNullOrWhiteSpace(x.Value)))
            {
                throw new ArgumentException("Every reasoning effort must have a value.");
            }

            var duplicateValue = model.ReasoningEfforts
                .GroupBy(
                    x => x.Value.Trim(),
                    StringComparer.OrdinalIgnoreCase)
                .Any(x => x.Count() > 1);

            if (duplicateValue)
            {
                throw new ArgumentException("Reasoning effort values must be unique.");
            }
        }
    }
}
