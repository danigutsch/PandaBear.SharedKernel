using Aspire.Hosting.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace PandaBear.Npgsql.Tests;

/// <summary>Adds a small long-running process that writes one line to its output, with a health check that is always healthy or never.</summary>
internal static class TalkingProcess
{
    private static readonly Action<ILogger, string, Exception?> WriteDiagnostic =
        LoggerMessage.Define<string>(LogLevel.Error, new EventId(1, "ModeledStartup"), "{Diagnostic}");

    /// <summary>Adds the process.</summary>
    /// <param name="builder">The test builder.</param>
    /// <param name="name">The resource name.</param>
    /// <param name="line">The line the process writes.</param>
    /// <param name="healthy">Whether its health check reports healthy.</param>
    /// <returns>The resource builder.</returns>
    internal static IResourceBuilder<ExecutableResource> Add(IDistributedApplicationTestingBuilder builder, string name, string line, bool healthy)
    {
        var check = name + "-check";
        _ = builder.Services.AddHealthChecks().AddCheck(check, () => healthy ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy());
        var process = OperatingSystem.IsWindows()
            ? builder.AddExecutable(name, "cmd", Environment.CurrentDirectory, "/c", $"echo {line} & ping -n 600 127.0.0.1 >nul")
            : builder.AddExecutable(name, "sh", Environment.CurrentDirectory, "-c", $"echo {line}; sleep 600");
        return process.WithHealthCheck(check);
    }

    /// <summary>Models an executable failing startup after writing diagnostics without starting a process.</summary>
    /// <param name="builder">The test builder.</param>
    /// <param name="stopped">Completes when the test application stops.</param>
    internal static void ModelStartupFailure(IDistributedApplicationTestingBuilder builder, TaskCompletionSource stopped)
    {
        var failed = Add(builder, "modeled-failure", "not-started", healthy: false).WithExplicitStart().Resource;
        var unrelated = Add(builder, "unrelated-resource", "not-started", healthy: true).WithExplicitStart().Resource;
        builder.Eventing.Subscribe<AfterResourcesCreatedEvent>(async (created, cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var lifetime = created.Services.GetRequiredService<IHostApplicationLifetime>();
            _ = lifetime.ApplicationStopped.Register(() => stopped.TrySetResult());
            var logs = created.Services.GetRequiredService<ResourceLoggerService>();
            var failedLogger = logs.GetLogger(failed);
            WriteDiagnostic(failedLogger, "evicted-startup-diagnostic", null);
            for (var count = 0; count < 49; count++)
            {
                WriteDiagnostic(failedLogger, "earlier-startup-progress", null);
            }

            WriteDiagnostic(failedLogger, "final-startup-diagnostic", null);
            WriteDiagnostic(logs.GetLogger(unrelated), "unrelated-resource-diagnostic", null);
            var notifications = created.Services.GetRequiredService<ResourceNotificationService>();
            await notifications.PublishUpdateAsync(failed, snapshot => snapshot with
            {
                State = new ResourceStateSnapshot(KnownResourceStates.FailedToStart, null),
                ExitCode = 17,
            });
        });
    }
}
