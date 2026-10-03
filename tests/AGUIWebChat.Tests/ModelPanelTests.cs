using System.Reflection;
using System.Collections.Concurrent;
using AGUIWebChat.Client.Components.AI;
using AGUIWebChat.Contracts.AI;
using AGUIWebChat.Contracts.AI.Models;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.FluentUI.AspNetCore.Components;
using Microsoft.JSInterop;
using Xunit;

namespace AGUIWebChat.Tests;

public sealed class ModelPanelTests
{
    [Theory]
    [InlineData("Temperature", "Temperature must be between 0 and 2.")]
    [InlineData("EffortName", "Reasoning effort display names cannot exceed 100 characters.")]
    [InlineData("Duplicate", "Reasoning effort values must be unique.")]
    [InlineData("Default", "Exactly one reasoning effort must be the default.")]
    public async Task Save_WhenInvalid_ShouldKeepDrawerOpenAndExposeMessages(string scenario, string message)
    {
        var model = CreateModel();
        switch (scenario)
        {
            case "Temperature": model.Temperature = 3; break;
            case "EffortName": model.ReasoningEfforts[0].DisplayName = new string('x', 101); break;
            case "Duplicate": model.ReasoningEfforts.Add(new() { DisplayName = "Duplicate", Value = " LOW " }); break;
            case "Default": model.ReasoningEfforts[0].IsDefault = false; break;
        }

        await using var harness = new PanelHarness(model);
        await harness.SaveAsync();

        Assert.False(harness.Dialog.Result.IsCompleted);
        Assert.Contains(message, harness.Context.GetValidationMessages());
    }

    [Fact]
    public async Task Save_AfterCorrectingAnError_ShouldClearMessagesAndCloseDrawer()
    {
        var model = CreateModel();
        model.Temperature = 3;
        await using var harness = new PanelHarness(model);
        await harness.SaveAsync();

        model.Temperature = null;
        harness.Context.NotifyFieldChanged(new FieldIdentifier(model, nameof(model.Temperature)));

        Assert.Empty(harness.Context.GetValidationMessages());
        await harness.SaveAsync();
        Assert.True(harness.Dialog.Result.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task Save_AfterChangingThinkingMode_ShouldIgnoreUnusedEfforts()
    {
        var model = CreateModel();
        model.ReasoningEfforts[0].Value = "";
        await using var harness = new PanelHarness(model);
        await harness.SaveAsync();
        Assert.False(harness.Dialog.Result.IsCompleted);

        model.ThinkingMode = ThinkingMode.None;
        harness.Context.NotifyFieldChanged(new FieldIdentifier(model, nameof(model.ThinkingMode)));
        await harness.SaveAsync();

        Assert.Empty(harness.Context.GetValidationMessages());
        Assert.True(harness.Dialog.Result.IsCompletedSuccessfully);
    }

    private static AIModelEditModel CreateModel() => new()
    {
        ProviderId = 1,
        ModelId = "test",
        DisplayName = "Test",
        ThinkingMode = ThinkingMode.Effort,
        ReasoningEfforts = [new() { DisplayName = "Low", Value = "low", IsDefault = true }]
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

        public PanelHarness(AIModelEditModel model)
        {
            var service = _services.GetRequiredService<IDialogService>();
            _instance = (IDialogInstance)Activator.CreateInstance(typeof(DialogInstance),
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
                binder: null, args: [service, typeof(ModelPanel), new DialogOptions()], culture: null)!;
            var dialogs = (ConcurrentDictionary<string, IDialogInstance>)service.GetType().BaseType!
                .GetField("_list", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(service)!;
            dialogs.TryAdd(_instance.Id, _instance);
            _body = new FluentDialogBody(_services.GetRequiredService<LibraryConfiguration>());
            typeof(FluentDialogBody).GetProperty("Instance", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
                .SetValue(_body, _instance);
#pragma warning disable BL0005 // Supply component parameters without rendering the Fluent UI controls.
            _panel = new TestPanel { Model = model, DialogInstance = _instance };
#pragma warning restore BL0005
            _panel.Initialize();
            Context = (EditContext)typeof(ModelPanel)
                .GetField("_editContext", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(_panel)!;
            // Register the same annotation validation supplied by DataAnnotationsValidator in the form.
            _annotations = Context.EnableDataAnnotationsValidation(_services);
        }

        public Task SaveAsync() => (Task)typeof(FluentDialogBody)
            .GetMethod("ActionClickHandlerAsync", BindingFlags.Instance | BindingFlags.NonPublic,
                [typeof(DialogOptionsFooterAction)])!
            .Invoke(_body, [_instance.Options.Footer.PrimaryAction])!;

        public async ValueTask DisposeAsync()
        {
            _annotations.Dispose();
            _panel.Dispose();
            await _services.DisposeAsync();
        }
    }

    private sealed class TestPanel : ModelPanel
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
