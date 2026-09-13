using AGUIWebChat.Client.Components;
using AGUIWebChat.Client.Middleware;
using AGUIWebChat.Client.Services;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.FluentUI.AspNetCore.Components;
using Serilog;
using System.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

// Use Serilog for logging requests and events
builder.Host.UseSerilog((context, configuration) =>
    configuration.ReadFrom.Configuration(context.Configuration));

// Use Microsoft Http logging traces
builder.Services.AddHttpLogging(logging =>
{
    logging.LoggingFields = HttpLoggingFields.None;

    logging.RequestBodyLogLimit = 64 * 1024;
    logging.ResponseBodyLogLimit = 64 * 1024;

    logging.CombineLogs = true;
});

string serverUrl = builder.Configuration["AGUI_SERVER_URL"] ?? "http://localhost:5100";

// First stratup
Log.Information("Starting AG-UI Web Chat Client");
Log.Information("Machine Name : {MachineName}", Environment.MachineName);
Log.Information("OS Version : {OSVersion}", Environment.OSVersion);
Log.Information("DotNet Version : {DotNetVersion}", Environment.Version);
Log.Information("UserName : {UserName}", Environment.UserName);
Log.Information("Process Id : {ProcessId}", Process.GetCurrentProcess().Id);
Log.Information("Process Name : {ProcessName}", Process.GetCurrentProcess().ProcessName);

// Add services to the container.
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddFluentUIComponents();
builder.Services.AddScoped(_ => new TelemetryService(new Uri(new Uri(serverUrl), "/telemetry").ToString()));

// AG-UI client SSE event logger
builder.Services.AddSingleton<IAgUiClientSseEventLogger, AgUiClientSseEventLogger>();
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
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
