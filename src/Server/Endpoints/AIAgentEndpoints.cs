using AGUIWebChat.Contracts.AI.Agents;
using AGUIWebChat.Server.Services.AI;

namespace AGUIWebChat.Server.Endpoints
{
    public static class AIAgentEndpoints
    {
        public static IEndpointRouteBuilder MapAIAgentEndpoints(this IEndpointRouteBuilder endpoints)
        {
            var group = endpoints.MapGroup("/api/agents").WithTags("AI Agents");

            group.MapGet("/", GetAgentsAsync).WithSummary("List AI agents").Produces<IReadOnlyList<AIAgentEditModel>>();
            group.MapGet("/{id:int}", GetAgentAsync).WithSummary("Get an AI agent").Produces<AIAgentEditModel>().ProducesProblem(404);
            group.MapPost("/", CreateAgentAsync).WithSummary("Create an AI agent").Produces<AIAgentEditModel>(201).ProducesValidationProblem().ProducesProblem(409);
            group.MapPut("/{id:int}", UpdateAgentAsync).WithSummary("Update an AI agent").Produces<AIAgentEditModel>().ProducesValidationProblem().ProducesProblem(404).ProducesProblem(409);
            group.MapDelete("/{id:int}", DeleteAgentAsync).WithSummary("Delete an AI agent").Produces(204).ProducesProblem(404);

            return endpoints;
        }

        private static async Task<IResult> GetAgentsAsync(IAIAgentService agentService, CancellationToken cancellationToken)
        {
            var agents = await agentService.GetAgentsAsync(cancellationToken: cancellationToken);

            return Results.Ok(agents);
        }

        private static async Task<IResult> GetAgentAsync(int id, IAIAgentService agentService, CancellationToken cancellationToken)
        {
            var agent = await agentService.GetAgentAsync(id, cancellationToken);

            return agent is null
                ? throw new ModelNotFoundException($"AI agent with id {id} was not found.")
                : Results.Ok(agent);
        }

        private static async Task<IResult> CreateAgentAsync(AIAgentEditModel agent, IAIAgentService agentService,
            CancellationToken cancellationToken)
        {
            var created = await agentService.CreateAsync(agent, cancellationToken);

            return Results.Created($"/api/agents/{created.Id}", created);
        }

        private static async Task<IResult> UpdateAgentAsync(int id, AIAgentEditModel agent, IAIAgentService agentService,
            CancellationToken cancellationToken)
        {
            if (id != agent.Id)
            {
                throw new ModelValidationException("The route id does not match the agent id.");
            }

            var updated = await agentService.UpdateAsync(agent, cancellationToken);

            return Results.Ok(updated);
        }

        private static async Task<IResult> DeleteAgentAsync(int id, IAIAgentService agentService, CancellationToken cancellationToken)
        {
            await agentService.DeleteAsync(id, cancellationToken);

            return Results.NoContent();
        }
    }
}
