using AGUIWebChat.Contracts.AI.Providers;
using AGUIWebChat.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace AGUIWebChat.Server.Endpoints
{
    public static class AIProviderEndpoints
    {
        public static IEndpointRouteBuilder MapAIProviderEndpoints(this IEndpointRouteBuilder endpoints)
        {
            endpoints.MapGet("/api/providers", async (ChatDbContext dbContext, CancellationToken cancellationToken) =>
            {
                var providers = await dbContext.AIProviders
                    .AsNoTracking()
                    .Where(x => x.IsEnabled)
                    .OrderBy(x => x.Name)
                    .Select(x => new AIProviderListItem
                    {
                        Id = x.Id,
                        Name = x.Name
                    })
                    .ToListAsync(cancellationToken);

                return Results.Ok(providers);
            })
                .WithTags("AI Providers")
                .WithSummary("List enabled AI providers")
                .Produces<IReadOnlyList<AIProviderListItem>>();

            return endpoints;
        }
    }
}
