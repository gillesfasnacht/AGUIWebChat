using AGUIWebChat.Contracts.AI;
using AGUIWebChat.Contracts.AI.Agents;
using AGUIWebChat.Server.Data;
using AGUIWebChat.Server.Domain.AI;
using Mapster;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace AGUIWebChat.Server.Services.AI
{
    public sealed class AIAgentService : IAIAgentService
    {
        private readonly ChatDbContext _dbContext;

        public AIAgentService(ChatDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<IReadOnlyList<AIAgentEditModel>> GetAgentsAsync(bool enabledOnly = false, 
            CancellationToken cancellationToken = default)
        {
            var query =_dbContext.AIAgents
                .AsNoTracking()
                .Include(x => x.AIModel)
                .AsQueryable();

            if (enabledOnly)
            {
                query = query.Where(x => x.IsEnabled);
            }

            var agents = await query.OrderBy(x => x.Name).ToListAsync(cancellationToken);

            return agents.Adapt<List<AIAgentEditModel>>();
        }

        public async Task<AIAgentEditModel?> GetAgentAsync(int id, CancellationToken cancellationToken = default)
        {
            var entity = await _dbContext.AIAgents
                .AsNoTracking()
                .Include(x => x.AIModel)
                .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

            return entity?.Adapt<AIAgentEditModel>();
        }

        public async Task<AIAgentEditModel> CreateAsync(AIAgentEditModel agent, CancellationToken cancellationToken = default)
        {
            Validate(agent);
            var normalizedName = agent.Name.Trim();

            var model = await GetModelAsync(agent.AIModelId, cancellationToken);

            await ValidateNameIsUniqueAsync(normalizedName, null, cancellationToken);

            ValidateReasoningEffort(agent, model);

            var entity = agent.Adapt<AIAgent>();

            // L'identité appartient à SQL Server.
            entity.Id = 0;

            entity.Name = normalizedName;
            entity.AIModel = model;

            _dbContext.AIAgents.Add(entity);

            await _dbContext.SaveChangesAsync(cancellationToken);

            return entity.Adapt<AIAgentEditModel>();
        }

        public async Task<AIAgentEditModel> UpdateAsync(AIAgentEditModel agent, CancellationToken cancellationToken = default)
        {
            Validate(agent);
            var normalizedName = agent.Name.Trim();

            var entity = await _dbContext.AIAgents
                .Include(x => x.AIModel)
                .FirstOrDefaultAsync(x => x.Id == agent.Id, cancellationToken);

            if (entity is null)
            {
                throw new ModelNotFoundException($"AI agent with id {agent.Id} was not found.");
            }

            var model = await GetModelAsync(agent.AIModelId, cancellationToken);

            await ValidateNameIsUniqueAsync(normalizedName, agent.Id, cancellationToken);

            ValidateReasoningEffort(agent, model);

            agent.Adapt(entity);

            entity.Name = normalizedName;
            entity.AIModel = model;

            await _dbContext.SaveChangesAsync(cancellationToken);

            return entity.Adapt<AIAgentEditModel>();
        }

        public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
        {
            var entity = await _dbContext.AIAgents.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

            if (entity is null)
            {
                throw new ModelNotFoundException($"AI agent with id {id} was not found.");
            }

            _dbContext.AIAgents.Remove(entity);

            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        private static void Validate(AIAgentEditModel agent)
        {
            var results = new List<ValidationResult>();
            if (!Validator.TryValidateObject(agent, new ValidationContext(agent), results, validateAllProperties: true))
            {
                throw new ModelValidationException(string.Join(" ", results.Select(x => x.ErrorMessage)));
            }
        }

        private async Task<AIModel> GetModelAsync(int modelId, CancellationToken cancellationToken)
        {
            var model = await _dbContext.AIModels
                .Include(x => x.ReasoningEfforts)
                .FirstOrDefaultAsync(x => x.Id == modelId, cancellationToken);

            if (model is null)
            {
                throw new ModelValidationException($"AI model with id {modelId} does not exist.");
            }

            if (!model.IsEnabled)
            {
                throw new ModelValidationException($"AI model '{model.DisplayName}' is disabled.");
            }

            return model;
        }

        private static void ValidateReasoningEffort(AIAgentEditModel agent, AIModel model)
        {
            if (string.IsNullOrWhiteSpace(agent.ReasoningEffort))
            {
                return;
            }

            if (model.ThinkingMode != ThinkingMode.Effort)
            {
                throw new ModelValidationException(
                    $"AI model '{model.DisplayName}' " +
                    "does not support reasoning effort levels.");
            }

            var exists = model.ReasoningEfforts.Any(x => string.Equals(
                x.Value,
                agent.ReasoningEffort,
                StringComparison.OrdinalIgnoreCase));

            if (!exists)
            {
                throw new ModelValidationException(
                    $"Reasoning effort '{agent.ReasoningEffort}' " +
                    $"is not supported by AI model '{model.DisplayName}'.");
            }
        }

        private async Task ValidateNameIsUniqueAsync(string name, int? excludedAgentId, CancellationToken cancellationToken)
        {
            var normalizedName = name.Trim();

            var exists = await _dbContext.AIAgents
                .AnyAsync(x =>
                    x.Name == normalizedName &&
                    (!excludedAgentId.HasValue || x.Id != excludedAgentId.Value),
                    cancellationToken);

            if (exists)
            {
                throw new ModelConflictException($"An AI agent named '{normalizedName}' already exists.");
            }
        }
    }
}
