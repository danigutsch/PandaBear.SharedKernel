using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;

namespace SharedKernel.HttpClients;

/// <summary>
/// Registers shared HTTP client defaults for service-discovered outbound clients.
/// </summary>
public static class HttpServiceCollectionExtensions
{
    /// <summary>
    /// Adds standard resilience, service discovery, and HTTP client telemetry to all configured <see cref="HttpClient" /> instances.
    /// </summary>
    /// <param name="services">The service collection to update.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddHttpClientDefaults(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.ConfigureHttpClientDefaults(http =>
        {
            http.AddStandardResilienceHandler();
            http.AddServiceDiscovery();
        });

        services.AddOpenTelemetry()
            .WithMetrics(metrics => metrics.AddHttpClientInstrumentation());

        return services;
    }
}
