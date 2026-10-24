using AGUIWebChat.Contracts.Chat;
using AGUIWebChat.Server.Services.Chat;
using Microsoft.AspNetCore.Mvc;

namespace AGUIWebChat.Server.Endpoints;

public static class ConversationEndpoints
{
    public static IEndpointRouteBuilder MapConversationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/conversations").WithTags("Conversations");

        group.MapGet("/", GetConversationsAsync).WithSummary("List this browser's conversations")
            .Produces<IReadOnlyList<ConversationDto>>().ProducesValidationProblem();
        group.MapPost("/", CreateConversationAsync).Produces<ConversationDto>(201).ProducesValidationProblem();
        group.MapGet("/{id:guid}", GetConversationAsync).Produces<ConversationDto>().ProducesProblem(404);
        group.MapGet("/{id:guid}/messages", GetMessagesAsync).Produces<IReadOnlyList<ChatMessageDto>>().ProducesProblem(404);
        group.MapPatch("/{id:guid}", RenameConversationAsync).Produces<ConversationDto>().ProducesValidationProblem().ProducesProblem(404);
        group.MapDelete("/{id:guid}", DeleteConversationAsync).Produces(204).ProducesProblem(404).ProducesProblem(409);

        return endpoints;
    }

    private static async Task<IResult> GetConversationsAsync(
        [FromHeader(Name = ConversationAccess.Header)] string browserKey,
        IConversationService service, CancellationToken token, int skip = 0, int take = 30)
    {
        var conversations = await service.ListAsync(ConversationAccess.GetOwnerKey(browserKey), skip, take, token);

        return Results.Ok(conversations);
    }

    private static async Task<IResult> CreateConversationAsync(
        [FromHeader(Name = ConversationAccess.Header)] string browserKey,
        IConversationService service, CancellationToken token)
    {
        var created = await service.CreateAsync(ConversationAccess.GetOwnerKey(browserKey), token);

        return Results.Created($"/api/conversations/{created.Id}", created);
    }

    private static async Task<IResult> GetConversationAsync(Guid id,
        [FromHeader(Name = ConversationAccess.Header)] string browserKey,
        IConversationService service, CancellationToken token)
    {
        var conversation = await service.GetAsync(ConversationAccess.GetOwnerKey(browserKey), id, token);

        return Results.Ok(conversation);
    }

    private static async Task<IResult> GetMessagesAsync(Guid id,
        [FromHeader(Name = ConversationAccess.Header)] string browserKey,
        IConversationService service, CancellationToken token)
    {
        var messages = await service.GetMessagesAsync(ConversationAccess.GetOwnerKey(browserKey), id, token);

        return Results.Ok(messages);
    }

    private static async Task<IResult> RenameConversationAsync(Guid id, RenameConversationRequest request,
        [FromHeader(Name = ConversationAccess.Header)] string browserKey,
        IConversationService service, CancellationToken token)
    {
        var updated = await service.RenameAsync(ConversationAccess.GetOwnerKey(browserKey), id, request, token);

        return Results.Ok(updated);
    }

    private static async Task<IResult> DeleteConversationAsync(Guid id,
        [FromHeader(Name = ConversationAccess.Header)] string browserKey,
        IConversationService service, CancellationToken token)
    {
        await service.DeleteAsync(ConversationAccess.GetOwnerKey(browserKey), id, token);

        return Results.NoContent();
    }
}
