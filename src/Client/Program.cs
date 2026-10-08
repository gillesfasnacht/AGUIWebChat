using AGUIWebChat.Client.Components;
using AGUIWebChat.Client.Middleware;
using AGUIWebChat.Client.Services;
using AGUIWebChat.Client.Services.AI;
using AGUIWebChat.Middleware;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.FluentUI.AspNetCore.Components;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

var serverUrl = builder.Configuration["AGUI_SERVER_URL"] ?? "http://localhost:5100";

// Use Serilog for logging requests and events
builder.Host.UseSerilog((context, configuration) =>
    configuration.ReadFrom.Configuration(context.Configuration));

// Add a hosted service to log application lifecycle events (start, stop, etc.)
builder.Services.AddHostedService<ApplicationLifecycleLogger>();

// Use Microsoft Http logging traces
builder.Services.AddHttpLogging(logging =>
{
    logging.LoggingFields = HttpLoggingFields.None;

    logging.RequestBodyLogLimit = 64 * 1024;
    logging.ResponseBodyLogLimit = 64 * 1024;

    logging.CombineLogs = true;
});

// Add services to the container.
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddFluentUIComponents();
builder.Services.AddScoped(_ => new TelemetryService(new Uri(new Uri(serverUrl), "/telemetry").ToString()));

// Add HttpClient for AIModelApiClient (/api/models)
builder.Services.AddHttpClient<AIModelApiClient>(client =>
{
    client.BaseAddress = new Uri(serverUrl);
});

// Add HttpClient for AIProviderApiClient (/api/providers/{id})
builder.Services.AddHttpClient<AIProviderApiClient>(client =>
{
    client.BaseAddress = new Uri(serverUrl);
});

// AG-UI client SSE event logger
builder.Services.AddSingleton<IAgUiSseEventLogger, AgUiClientSseEventLogger>();
builder.Services.AddTransient<AgUiHttpLoggingHandler>();
builder.Services.AddScoped<ChatSettingsService>();
builder.Services.AddHttpClient<AgUiChatService>(client =>
{
    client.BaseAddress = new Uri(serverUrl);
}).AddHttpMessageHandler<AgUiHttpLoggingHandler>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

app.Run();
