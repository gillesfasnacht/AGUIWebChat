using System.Reflection;
using System.Collections.Concurrent;
using AGUIWebChat.Client.Components.AI;
using AGUIWebChat.Contracts.AI.Agents;
using AGUIWebChat.Contracts.AI.Models;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.FluentUI.AspNetCore.Components;
using Microsoft.JSInterop;
using Xunit;

namespace AGUIWebChat.Client.Tests;

public sealed class AgentPanelTests
{
    [Theory]
    [InlineData("name", "Agent name is required.")]
    [InlineData("prompt", "System prompt is required.")]
    [InlineData("temperature", "Temperature must be between 0 and 2.")]
    [InlineData("top-p", "TopP must be between 0 and 1.")]
    public async Task InvalidField_ShouldHaveOneMessageAndKeepDrawerOpen(string field, string message)
    {
        var agent = CreateAgent();

        switch (field)
        {
            case "name": agent.Name = " "; break;
            case "prompt": agent.SystemPrompt = " "; break;
            case "temperature": agent.Temperature = 3; break;
            case "top-p": agent.TopP = 2; break;
        }

        await using var harness = new PanelHarness(agent);

        await harness.SaveAsync();

        Assert.False(harness.Dialog.Result.IsCompleted);
        Assert.Equal(1, harness.Context.GetValidationMessages().Count(x => x == message));
    }

    [Fact]
    public async Task Save_WhenApiRejectsAgent_ShouldKeepDrawerOpenAndAllowCorrection()
    {
        var agent = CreateAgent();

        await using var harness = new PanelHarness(agent, () =>
        {
            if (agent.Name == "Duplicate")
                throw new AGUIWebChat.Client.Services.AI.ApiRequestException("Agent already exists.",
                    System.Net.HttpStatusCode.Conflict, ["Agent already exists."]);
            return Task.CompletedTask;
        });

        await harness.SaveAsync();

        Assert.False(harness.Dialog.Result.IsCompleted);
        Assert.Contains("Agent already exists.", harness.Context.GetValidationMessages());

        agent.Name = "Unique";
        harness.Context.NotifyFieldChanged(new FieldIdentifier(agent, nameof(agent.Name)));

        Assert.Empty(harness.Context.GetValidationMessages());

        await harness.SaveAsync();

        Assert.True(harness.Dialog.Result.IsCompletedSuccessfully);
    }

    private static AIAgentEditModel CreateAgent() => new()
    {
        Name = "Duplicate", SystemPrompt = "Test.", AIModelId = 1
    };

    private sealed class PanelHarness : IAsyncDisposable
    {
        private readonly TestPanel _panel;
        private readonly ServiceProvider _services = new ServiceCollection().AddLogging()
            .AddSingleton<IJSRuntime, TestJsRuntime>().AddFluentUIComponents().BuildServiceProvider();
        private readonly IDisposable _annotations;
        private readonly IDialogInstance _instance;
        private readonly FluentDialogBody _body;

        public EditContext Context { get; }
        public IDialogInstance Dialog => _instance;

        public PanelHarness(AIAgentEditModel agent, Func<Task>? save = null)
        {
            var service = _services.GetRequiredService<IDialogService>();

            _instance = (IDialogInstance)Activator.CreateInstance(typeof(DialogInstance),
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
                binder: null, args: [service, typeof(AgentPanel), new DialogOptions()], culture: null)!;

            var dialogs = (ConcurrentDictionary<string, IDialogInstance>)service.GetType().BaseType!
                .GetField("_list", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(service)!;

            dialogs.TryAdd(_instance.Id, _instance);

            _body = new FluentDialogBody(_services.GetRequiredService<LibraryConfiguration>());

            typeof(FluentDialogBody).GetProperty("Instance", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
                .SetValue(_body, _instance);

#pragma warning disable BL0005 // Supply component parameters without rendering the Fluent UI controls.
            _panel = new TestPanel { Agent = agent, DialogInstance = _instance, SaveAsync = save, 
                Models = [new AIModelEditModel { Id = 1, IsEnabled = true }] };
#pragma warning restore BL0005

            _panel.Initialize();

            Context = (EditContext)typeof(AgentPanel)
                .GetField("_editContext", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(_panel)!;

            // Register the same annotation validation supplied by DataAnnotationsValidator in the form.
            _annotations = Context.EnableDataAnnotationsValidation(_services);
        }

        public Task SaveAsync() => (Task)typeof(FluentDialogBody)
            .GetMethod("ActionClickHandlerAsync", BindingFlags.Instance | BindingFlags.NonPublic, [typeof(DialogOptionsFooterAction)])!
            .Invoke(_body, [_instance.Options.Footer.PrimaryAction])!;

        public async ValueTask DisposeAsync()
        {
            _annotations.Dispose();
            _panel.Dispose();
            await _services.DisposeAsync();
        }
    }

    private sealed class TestPanel : AgentPanel
    {
        public void Initialize() => base.OnInitialized();
    }

    private sealed class TestJsRuntime : IJSRuntime, IJSObjectReference
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
            => InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
            => ValueTask.FromResult(typeof(TValue) == typeof(IJSObjectReference) ? (TValue)(object)this : default!);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

}
