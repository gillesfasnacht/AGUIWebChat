using AGUI.Abstractions;
using AGUI.Server;
using AGUIFluentUIChatClient.Midleware;
using AGUIWebChatServer.Hubs;
using AGUIWebChatServer.Inference;
using AGUIWebChatServer.Midleware;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting.AGUI.AspNetCore;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.AI;
using OllamaSharp;
using OpenTelemetry;
using OpenTelemetry.Trace;
using Serilog;
using System.Diagnostics;
using System.Text.Json;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Configuration for Ollama local
var ollamaEndpoint = Environment.GetEnvironmentVariable("OLLAMA_ENDPOINT") ?? throw new InvalidOperationException("OLLAMA_ENDPOINT is not set.");
var modelName = Environment.GetEnvironmentVariable("OLLAMA_MODEL") ?? "granite4.2:8b";

// Create an OllamaApiClient with logging
var ollamaHandlerClient = new OllamaHttpLoggingHandler
{
    InnerHandler = new HttpClientHandler()
};
var ollamaHttpClient = new HttpClient(ollamaHandlerClient)
{
    BaseAddress = new Uri(ollamaEndpoint)
};
var ollamaApiClient = new OllamaApiClient(ollamaHttpClient, modelName);

// Use Serilog for logging requests and events
builder.Host.UseSerilog((context, configuration) =>
    configuration.ReadFrom.Configuration(context.Configuration));

// Add SignalR for real-time communication
builder.Services.AddSignalR();

// Use Microsoft Http logging traces
builder.Services.AddHttpLogging(logging =>
{
    logging.LoggingFields =
        HttpLoggingFields.RequestMethod |
        HttpLoggingFields.RequestPath |
        HttpLoggingFields.RequestHeaders |
        HttpLoggingFields.RequestBody |
        HttpLoggingFields.ResponseStatusCode;

    logging.RequestBodyLogLimit = 64 * 1024;

    logging.MediaTypeOptions.AddText("application/json");

    logging.CombineLogs = true;
});

// First stratup
Log.Information("Starting AG-UI Web Chat Server");
Log.Information("Machine Name : {MachineName}", Environment.MachineName);
Log.Information("OS Version : {OSVersion}", Environment.OSVersion);
Log.Information("DotNet Version : {DotNetVersion}", Environment.Version);
Log.Information("UserName : {UserName}", Environment.UserName);
Log.Information("Process Id : {ProcessId}", Process.GetCurrentProcess().Id);
Log.Information("Process Name : {ProcessName}", Process.GetCurrentProcess().ProcessName);

builder.Services.AddHttpLoggingInterceptor<AgUiHttpLoggingInterceptor>();
builder.Services.AddSingleton<IAgUiSseEventLogger, AgUiSseEventLogger>();

// Ajouter les services AG-UI pour la gestion du protocole
builder.Services.AddAGUIServer();

WebApplication app = builder.Build();

using var enterpriseSupportTracerProvider = CreateTraceProviderConsole("EnterpriseSupportAgenceSource",
        ReasoningTelemetry.ActivitySourceName);

var telemetryHub = app.Services.GetRequiredService<IHubContext<TelemetryHub>>();

AIAgent enterpriseAgent = CreateAgent(
    ollamaApiClient,
    name: "EnterpriseSupportAgent",
    instruction: """
        You are a helpful business support agent
        """,
    source: "EnterpriseSupportAgenceSource",
    modelName: modelName,
    defaultSettings: new InferenceSettings
    {
        ThinkingEffort = "low",
        Temperature = 0.7,
        TopP = 0.9,
        TopK = 40,
        NumCtx = 16384
    },
    telemetryHub: telemetryHub);

app.UseHttpLogging();

app.MapHub<TelemetryHub>("/telemetry");

app.UseMiddleware<SseLoggingMiddleware>();

app.MapAGUIServer("/ag-ui", enterpriseAgent);

await app.RunAsync();

static AIAgent CreateAgent(
    IChatClient chatClient,
    string name,
    string instruction,
    string source,
    string modelName,
    InferenceSettings defaultSettings,
    IHubContext<TelemetryHub> telemetryHub)
{
    Log.Debug("SERVER AGENT_CREATION for {AgentName} with ActivitySource {ActivitySource}", name, source);

    return chatClient
        .AsAIAgent(
            new ChatClientAgentOptions
            {
                Name = name,
                ChatOptions = new ChatOptions
                {
                    Instructions = instruction,

                    AdditionalProperties = new()
                    {
                        ["think"] = defaultSettings.ThinkingEffort,
                        ["temperature"] = defaultSettings.Temperature,
                        ["top_p"] = defaultSettings.TopP,
                        ["top_k"] = defaultSettings.TopK,
                        ["num_ctx"] = defaultSettings.NumCtx
                    }
                }
            })
        .AsBuilder()
        .UseOpenTelemetry(sourceName: source)
        .Use(runFunc: null,
            runStreamingFunc:
                (messages, session, options, innerAgent, cancellationToken) =>
                {
                    var effectiveSettings =
                        GetInferenceSettings(
                            options,
                            defaultSettings);

                    var effectiveOptions =
                        ApplyInferenceSettings(
                            options,
                            effectiveSettings);

                    Log.Information("SERVER INFERENCE_SETTINGS Agent={AgentName} Model={ModelName} Effort={ThinkingEffort} Temperature={Temperature} TopP={TopP} TopK={TopK} NumCtx={NumCtx}",
                        name,
                        modelName,
                        effectiveSettings.ThinkingEffort,
                        effectiveSettings.Temperature,
                        effectiveSettings.TopP,
                        effectiveSettings.TopK,
                        effectiveSettings.NumCtx);

                    string runId = string.Empty;

                    if (effectiveOptions is ChatClientAgentRunOptions { ChatOptions: { } chatOptions } &&
                        chatOptions.TryGetRunAgentInput(out RunAgentInput? input))
                    {
                        runId = input.RunId;
                    }

                    var reasoningStream =
                       ReasoningTelemetry.ObserveAsync(
                           messages,
                           session,
                           effectiveOptions,
                           innerAgent,
                           telemetryHub,
                           runId,
                           name,
                           modelName,
                           effectiveSettings.ThinkingEffort,
                           cancellationToken);

                    return LlmTelemetry.ObserveAsync(
                        reasoningStream,
                        telemetryHub,
                        runId,
                        name,
                        modelName,
                        cancellationToken);
                })
        .Build();
}

static InferenceSettings GetInferenceSettings(AgentRunOptions? options, InferenceSettings defaults)
{
    if (options is not ChatClientAgentRunOptions
        {
            ChatOptions: { } chatOptions
        })
    {
        return defaults;
    }

    if (!chatOptions.TryGetRunAgentInput(out RunAgentInput? input))
    {
        return defaults;
    }

    if (input.State is not
        {
            ValueKind: JsonValueKind.Object
        } state)
    {
        return defaults;
    }

    string thinkingEffort = defaults.ThinkingEffort;
    double temperature = defaults.Temperature;
    double topP = defaults.TopP;
    int topK = defaults.TopK;
    int numCtx = defaults.NumCtx;

    if (state.TryGetProperty("thinkingEffort", out var thinkingElement))
    {
        thinkingEffort = thinkingElement.GetString()?.ToLowerInvariant() switch
        {
            "low" => "low",
            "medium" => "medium",
            "high" => "high",
            _ => defaults.ThinkingEffort
        };
    }

    if (state.TryGetProperty("temperature", out var temperatureElement) &&
        temperatureElement.TryGetDouble(out var parsedTemperature) &&
        parsedTemperature is >= 0 and <= 2)
    {
        temperature = parsedTemperature;
    }

    if (state.TryGetProperty("topP", out var topPElement) &&
        topPElement.TryGetDouble(out var parsedTopP) &&
        parsedTopP is >= 0 and <= 1)
    {
        topP = parsedTopP;
    }

    if (state.TryGetProperty("topK", out var topKElement) &&
        topKElement.TryGetInt32(out var parsedTopK) &&
        parsedTopK >= 1)
    {
        topK = parsedTopK;
    }

    if (state.TryGetProperty("numCtx", out var numCtxElement) &&
        numCtxElement.TryGetInt32(out var parsedNumCtx) &&
        parsedNumCtx >= 1024)
    {
        numCtx = parsedNumCtx;
    }

    return new InferenceSettings
    {
        ThinkingEffort = thinkingEffort,
        Temperature = temperature,
        TopP = topP,
        TopK = topK,
        NumCtx = numCtx
    };
}

static AgentRunOptions ApplyInferenceSettings(AgentRunOptions? options, InferenceSettings settings)
{
    ChatClientAgentRunOptions runOptions;

    if (options is ChatClientAgentRunOptions chatClientOptions)
    {
        runOptions = (ChatClientAgentRunOptions)chatClientOptions.Clone();
    }
    else
    {
        runOptions = new ChatClientAgentRunOptions();
    }

    runOptions.ChatOptions ??= new ChatOptions();
    runOptions.ChatOptions.AdditionalProperties ??= new();
    runOptions.ChatOptions.AdditionalProperties["think"] = settings.ThinkingEffort;
    runOptions.ChatOptions.AdditionalProperties["temperature"] =  settings.Temperature;
    runOptions.ChatOptions.AdditionalProperties["top_p"] = settings.TopP;
    runOptions.ChatOptions.AdditionalProperties["top_k"] = settings.TopK;
    runOptions.ChatOptions.AdditionalProperties["num_ctx"] = settings.NumCtx;

    return runOptions;
}

static TracerProvider CreateTraceProviderConsole(params string[] sourceNames)
{
    Log.Information("SERVER TRACE_PROVIDER console for {ActivitySources}",
        string.Join(", ", sourceNames));

    return Sdk.CreateTracerProviderBuilder()
        .AddSource(sourceNames)
        .SetSampler(new AlwaysOnSampler())
        .AddConsoleExporter()
        .Build();
}