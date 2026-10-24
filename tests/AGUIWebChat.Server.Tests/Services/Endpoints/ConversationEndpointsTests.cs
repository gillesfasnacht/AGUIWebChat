using AGUIWebChat.Contracts.Chat;
using AGUIWebChat.Server.Services.Chat;
using AGUIWebChat.Server.Tests.Infrastructure;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace AGUIWebChat.Server.Tests.Services.Endpoints;

public sealed class ConversationEndpointsTests
{
    [Fact]
    public async Task Crud_ShouldPersistRenameAndIsolateBrowsers()
    {
        var token = TestContext.Current.CancellationToken;
        await using var factory = new AGUIWebChatWebApplicationFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(ConversationAccess.Header, Guid.NewGuid().ToString("N"));
        using var createdResponse = await client.PostAsync("/api/conversations", null, token);
        Assert.Equal(HttpStatusCode.Created, createdResponse.StatusCode);
        var created = await createdResponse.Content.ReadFromJsonAsync<ConversationDto>(token);
        Assert.NotNull(created);
        using var renamedResponse = await client.PatchAsJsonAsync($"/api/conversations/{created.Id}",
            new RenameConversationRequest { Title = "  Invoice review  " }, token);
        Assert.Equal(HttpStatusCode.OK, renamedResponse.StatusCode);
        var renamed = await renamedResponse.Content.ReadFromJsonAsync<ConversationDto>(token);
        Assert.Equal("Invoice review", renamed!.Title);
        var list = await client.GetFromJsonAsync<List<ConversationDto>>("/api/conversations", token);
        Assert.Single(list!);

        using var other = factory.CreateClient();
        other.DefaultRequestHeaders.Add(ConversationAccess.Header, Guid.NewGuid().ToString("N"));
        Assert.Empty((await other.GetFromJsonAsync<List<ConversationDto>>("/api/conversations", token))!);
        using var hidden = await other.GetAsync($"/api/conversations/{created.Id}/messages", token);
        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        using var forbiddenRename = await other.PatchAsJsonAsync($"/api/conversations/{created.Id}",
            new RenameConversationRequest { Title = "Other" }, token);
        Assert.Equal(HttpStatusCode.NotFound, forbiddenRename.StatusCode);
        using var forbiddenDelete = await other.DeleteAsync($"/api/conversations/{created.Id}", token);
        Assert.Equal(HttpStatusCode.NotFound, forbiddenDelete.StatusCode);

        using var deleted = await client.DeleteAsync($"/api/conversations/{created.Id}", token);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        using var missing = await client.GetAsync($"/api/conversations/{created.Id}", token);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Theory]
    [InlineData("/api/conversations?take=101", true)]
    [InlineData("/api/conversations?skip=-1", true)]
    [InlineData("/api/conversations", false)]
    public async Task InvalidRequests_ShouldReturnProblemDetails(string path, bool addKey)
    {
        await using var factory = new AGUIWebChatWebApplicationFactory();
        using var client = factory.CreateClient();
        if (addKey) client.DefaultRequestHeaders.Add(ConversationAccess.Header, Guid.NewGuid().ToString("N"));
        using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }
}
