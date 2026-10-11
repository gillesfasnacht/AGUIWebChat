using AGUIWebChat.Client.Services.AI;
using AGUIWebChat.Contracts.AI.Agents;
using AGUIWebChat.Contracts.AI.Models;
using System.Net;
using System.Text;
using Xunit;

namespace AGUIWebChat.Client.Tests;

public sealed class AIApiClientTests
{
    [Fact]
    public async Task GetAgent_WhenNotFound_ShouldReturnNull()
    {
        using var http = CreateHttp(HttpStatusCode.NotFound, "");

        Assert.Null(await new AIAgentApiClient(http).GetAgentAsync(123, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("agent", 400, """{"detail":"Invalid agent.","errors":{"model":["Name is required.","TopP is invalid."]}}""", "Name is required. TopP is invalid.")]
    [InlineData("model", 409, """{"detail":"Model already exists."}""", "Model already exists.")]
    [InlineData("agent", 500, "<html>Error</html>", "The API request failed (500 Internal Server Error).")]
    [InlineData("model", 400, "", "The API request failed (400 Bad Request).")]
    public async Task Create_WhenRejected_ShouldPreservePreciseErrors(string resource, int status, string body, string message)
    {
        using var http = CreateHttp((HttpStatusCode)status, body);

        var token = TestContext.Current.CancellationToken;
        var error = await Assert.ThrowsAsync<ApiRequestException>(async () =>
        {
            if (resource == "agent") await new AIAgentApiClient(http).CreateAsync(new AIAgentEditModel(), token);
            else await new AIModelApiClient(http).CreateAsync(new AIModelEditModel(), token);
        });

        Assert.Equal((HttpStatusCode)status, error.StatusCode);
        Assert.Equal(message, error.Message);
        Assert.NotEmpty(error.Errors);
    }

    private static HttpClient CreateHttp(HttpStatusCode status, string body) => new(new Handler(status, body))
    {
        BaseAddress = new Uri("http://localhost")
    };

    private sealed class Handler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
            => Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/problem+json")
            });
    }
}
