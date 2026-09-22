using AGUIWebChat.Contracts.AI.Models;

namespace AGUIWebChat.Server.Services.AI
{
    public interface IAIModelService
    {
        Task<IReadOnlyList<AIModelEditModel>> GetModelsAsync(
            bool enabledOnly = false,
            CancellationToken cancellationToken = default);

        Task<AIModelEditModel?> GetModelAsync(
            int id,
            CancellationToken cancellationToken = default);

        Task<AIModelEditModel?> GetModelByModelIdAsync(
            int providerId,
            string modelId,
            CancellationToken cancellationToken = default);

        Task<AIModelEditModel> CreateAsync(
            AIModelEditModel model,
            CancellationToken cancellationToken = default);

        Task<AIModelEditModel> UpdateAsync(
            AIModelEditModel model,
            CancellationToken cancellationToken = default);

        Task DeleteAsync(
            int id,
            CancellationToken cancellationToken = default);
    }
}
