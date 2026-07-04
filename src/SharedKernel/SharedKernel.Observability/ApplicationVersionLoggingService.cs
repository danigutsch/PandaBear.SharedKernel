using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SharedKernel.Observability;

internal sealed class ApplicationVersionLoggingService(
    string applicationName,
    string? applicationVersion,
    ILogger<ApplicationVersionLoggingService> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        logger.ApplicationVersion(applicationName, string.IsNullOrWhiteSpace(applicationVersion) ? "unknown" : applicationVersion);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
