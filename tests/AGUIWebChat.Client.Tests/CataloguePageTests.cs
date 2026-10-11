using AGUIWebChat.Client.Components.Pages;
using AGUIWebChat.Client.Services.AI;
using System.Net;
using System.Reflection;
using System.Text;
using Xunit;

namespace AGUIWebChat.Client.Tests;

public sealed class CataloguePageTests
{
    [Theory]
    [InlineData("agents")]
    [InlineData("models")]
    public async Task Load_AfterFailure_ShouldPreserveListAndAllowRetry(string resource)
    {
        var handler = new SequenceHandler(resource);

        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };

        object page = resource == "agents" ? new Agents() : new AGUIWebChat.Client.Components.Pages.Models();

        var type = page.GetType();
        var singular = resource == "agents" ? "Agent" : "Model";

        object api = resource == "agents" ? new AIAgentApiClient(http) : new AIModelApiClient(http);

        type.GetProperty($"{singular}ApiClient", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)!
            .SetValue(page, api);

        var load = type.GetMethod($"Load{singular}sAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var rows = type.GetField($"_{resource}", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var failed = type.GetField("_loadFailed", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var error = type.GetField("_errorMessage", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var loading = type.GetField("_loading", BindingFlags.Instance | BindingFlags.NonPublic)!;

        await (Task)load.Invoke(page, null)!;

        var previousRows = rows.GetValue(page);

        Assert.False((bool)failed.GetValue(page)!);

        await (Task)load.Invoke(page, null)!;

        Assert.Same(previousRows, rows.GetValue(page));
        Assert.True((bool)failed.GetValue(page)!);
        Assert.Contains("Temporarily unavailable.", (string)error.GetValue(page)!);
        Assert.False((bool)loading.GetValue(page)!);

        await (Task)load.Invoke(page, null)!;

        Assert.False((bool)failed.GetValue(page)!);
        Assert.Null(error.GetValue(page));
        Assert.False((bool)loading.GetValue(page)!);
        Assert.NotSame(previousRows, rows.GetValue(page));
    }

    private sealed class SequenceHandler(string resource) : HttpMessageHandler
    {
        private int _requests;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            var failure = ++_requests == 2;
            var body = failure ? """{"detail":"Temporarily unavailable."}"""
                : resource == "agents" ? """[{"id":1,"name":"Agent"}]"""
                : """[{"id":1,"displayName":"Model"}]""";

            return Task.FromResult(new HttpResponseMessage(failure ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, failure ? "application/problem+json" : "application/json")
            });
        }
    }
}

