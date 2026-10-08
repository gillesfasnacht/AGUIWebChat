using AGUIWebChat.Server.Telemetry;
using System.Diagnostics;

namespace AGUIWebChat.Server.Middleware
{
    public sealed class ApplicationLifecycleLogger(ILogger<ApplicationLifecycleLogger> logger) : IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken)
        {
            logger.LogInformation("Starting AG-UI Web Chat Server");
            logger.LogInformation("Machine Name : {MachineName}", Environment.MachineName);
            logger.LogInformation("OS Version : {OSVersion}", Environment.OSVersion);
            logger.LogInformation("DotNet Version : {DotNetVersion}", Environment.Version);
            logger.LogInformation("UserName : {UserName}", Environment.UserName);
            logger.LogInformation("Process Id : {ProcessId}", Environment.ProcessId);
            logger.LogInformation("Process Name : {ProcessName}", Process.GetCurrentProcess().ProcessName);
            logger.LogInformation("OpenTelemetry ActivitySources : {ActivitySources}",
                string.Join(", ", ["EnterpriseSupportAgenceSource", ReasoningTelemetry.ActivitySourceName]));

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            logger.LogInformation("AG-UI Web Chat Server stopped");

            return Task.CompletedTask;
        }
    }
}
