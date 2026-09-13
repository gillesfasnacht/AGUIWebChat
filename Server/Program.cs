using AGUIWebChat.Server.Telemetry;
using AGUIWebChat.Server.Agents;
using AGUIWebChat.Server.Hubs;
using AGUIWebChat.Server.Inference;
using AGUIWebChat.Server.Middleware;
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

AIAgent enterpriseAgent = ChatAgentFactory.CreateAgent(
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