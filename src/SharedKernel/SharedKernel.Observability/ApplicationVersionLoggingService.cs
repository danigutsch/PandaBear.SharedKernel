using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace PandaBear.Observability;

/// <summary>
/// Hosted service that logs the application name and version once at startup, so deployed versions are visible in logs.
/// </summary>
/// <param name="applicationName">Name written to the log.</param>
/// <param name="applicationVersion">Version written to the log; null or blank logs "unknown".</param>
/// <param name="logger">Logger that receives the message.</param>
internal sealed class ApplicationVersionLoggingService(
    string applicationName,
    string? applicationVersion,
    ILogger<ApplicationVersionLoggingService> logger) : IHostedService
{
    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        logger.ApplicationVersion(applicationName, string.IsNullOrWhiteSpace(applicationVersion) ? "unknown" : applicationVersion);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
