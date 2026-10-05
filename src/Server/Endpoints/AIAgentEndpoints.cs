using AGUIWebChat.Contracts.AI.Agents;
using AGUIWebChat.Server.Services.AI;

namespace AGUIWebChat.Server.Endpoints
{
    public static class AIAgentEndpoints
    {
        public static IEndpointRouteBuilder MapAIAgentEndpoints(this IEndpointRouteBuilder endpoints)
        {
            var group = endpoints.MapGroup("/api/agents").WithTags("AI Agents");

            group.MapGet("/", async (IAIAgentService service, CancellationToken cancellationToken) =>
            {
                var agents = await service.GetAgentsAsync(cancellationToken: cancellationToken);

                return Results.Ok(agents);
            });

            group.MapGet("/{id:int}", async (int id, IAIAgentService service, CancellationToken cancellationToken) =>
            {
                var agent = await service.GetAgentAsync(id, cancellationToken);

                return agent is null ? Results.NotFound() : Results.Ok(agent);
            });

            group.MapPost("/", async (AIAgentEditModel model, IAIAgentService service, CancellationToken cancellationToken) =>
            {
                var created = await service.CreateAsync(model, cancellationToken);

                return Results.Created($"/api/agents/{created.Id}", created);
            });

            group.MapPut("/{id:int}", async (int id, AIAgentEditModel model, IAIAgentService service, CancellationToken cancellationToken) =>
            {
                if (id != model.Id)
                {
                    return Results.BadRequest();
                }

                var updated = await service.UpdateAsync(model, cancellationToken);

                return Results.Ok(updated);
            });

            group.MapDelete("/{id:int}", async (int id, IAIAgentService service, CancellationToken cancellationToken) =>
            {
                await service.DeleteAsync(id, cancellationToken);

                return Results.NoContent();
            });

            return endpoints;
        }
    }
}
