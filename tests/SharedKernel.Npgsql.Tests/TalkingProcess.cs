using Aspire.Hosting.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace PandaBear.Npgsql.Tests;

/// <summary>Adds a small long-running process that writes one line to its output, with a health check that is always healthy or never.</summary>
internal static class TalkingProcess
{
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
}
