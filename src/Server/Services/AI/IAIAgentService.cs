using AGUIWebChat.Contracts.AI.Agents;

namespace AGUIWebChat.Server.Services.AI
{
    public interface IAIAgentService
    {
        Task<IReadOnlyList<AIAgentEditModel>> GetAgentsAsync(bool enabledOnly = false, CancellationToken cancellationToken = default);

        Task<AIAgentEditModel?> GetAgentAsync(int id, CancellationToken cancellationToken = default);

        Task<AIAgentEditModel> CreateAsync(AIAgentEditModel agent, CancellationToken cancellationToken = default);

        Task<AIAgentEditModel> UpdateAsync(AIAgentEditModel agent, CancellationToken cancellationToken = default);

        Task DeleteAsync(int id, CancellationToken cancellationToken = default);
    }
}
