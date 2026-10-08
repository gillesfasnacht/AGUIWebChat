using System.Diagnostics;

namespace AGUIWebChat.Client.Middleware
{
    public sealed class ApplicationLifecycleLogger(ILogger<ApplicationLifecycleLogger> logger) : IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken)
        {
            logger.LogInformation("Starting AG-UI Web Chat Client");
            logger.LogInformation("Machine Name : {MachineName}", Environment.MachineName);
            logger.LogInformation("OS Version : {OSVersion}", Environment.OSVersion);
            logger.LogInformation("DotNet Version : {DotNetVersion}", Environment.Version);
            logger.LogInformation("UserName : {UserName}", Environment.UserName);
            logger.LogInformation("Process Id : {ProcessId}", Environment.ProcessId);
            logger.LogInformation("Process Name : {ProcessName}", Process.GetCurrentProcess().ProcessName);

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            logger.LogInformation("AG-UI Web Chat Client stopped");

            return Task.CompletedTask;
        }
    }
}
