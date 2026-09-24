using AGUIWebChat.Contracts.AI.Models;
using AGUIWebChat.Server.Services.AI;

namespace AGUIWebChat.Server.Endpoints
{
    public static class AIModelEndpoints
    {
        public static IEndpointRouteBuilder MapAIModelEndpoints(this IEndpointRouteBuilder endpoints)
        {
            var group = endpoints
                .MapGroup("/api/models")
                .WithTags("AI Models");

            group.MapGet("/", GetModelsAsync);
            group.MapGet("/{id:int}", GetModelAsync);
            group.MapPost("/", CreateModelAsync);
            group.MapPut("/{id:int}", UpdateModelAsync);
            group.MapDelete("/{id:int}", DeleteModelAsync);

            return endpoints;
        }

        private static async Task<IResult> GetModelsAsync(
            IAIModelService modelService,
            bool enabledOnly = false,
            CancellationToken cancellationToken = default)
        {
            var models = await modelService.GetModelsAsync(enabledOnly, cancellationToken);

            return Results.Ok(models);
        }

        private static async Task<IResult> GetModelAsync(
            int id,
            IAIModelService modelService,
            CancellationToken cancellationToken)
        {
            var model = await modelService.GetModelAsync(id, cancellationToken);

            return model is null
                ? throw new ModelNotFoundException($"AI model with id {id} was not found.")
                : Results.Ok(model);
        }

        private static async Task<IResult> CreateModelAsync(
            AIModelEditModel model,
            IAIModelService modelService,
            CancellationToken cancellationToken)
        {
            var created = await modelService.CreateAsync(model, cancellationToken);

            return Results.Created($"/api/models/{created.Id}", created);
        }

        private static async Task<IResult> UpdateModelAsync(
            int id,
            AIModelEditModel model,
            IAIModelService modelService,
            CancellationToken cancellationToken)
        {
            if (id != model.Id)
            {
                throw new ModelValidationException("The route id does not match the model id.");
            }

            var updated = await modelService.UpdateAsync(model, cancellationToken);

            return Results.Ok(updated);
        }

        private static async Task<IResult> DeleteModelAsync(
            int id,
            IAIModelService modelService,
            CancellationToken cancellationToken)
        {
            await modelService.DeleteAsync(id, cancellationToken);

            return Results.NoContent();
        }
    }
}
