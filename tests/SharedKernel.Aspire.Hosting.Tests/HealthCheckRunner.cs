using Aspire.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace PandaBear.Aspire.Hosting.Tests;

/// <summary>Runs a registered health check of an application builder.</summary>
internal static class HealthCheckRunner
{
    /// <summary>Resolves the registered health check with the given name and runs it.</summary>
    /// <param name="builder">The application builder whose services hold the registration.</param>
    /// <param name="name">The registration name.</param>
    /// <returns>The health check result.</returns>
    public static async Task<HealthCheckResult> Run(IDistributedApplicationBuilder builder, string name)
    {
        using var provider = builder.Services.BuildServiceProvider();
        var registration = provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations.Single(r => r.Name == name);
        var check = registration.Factory(provider);
        return await check.CheckHealthAsync(new HealthCheckContext { Registration = registration }, TestContext.Current.CancellationToken);
    }
}
