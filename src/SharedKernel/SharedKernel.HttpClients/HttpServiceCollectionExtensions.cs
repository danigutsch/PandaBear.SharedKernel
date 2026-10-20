using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace PandaBear.HttpClients;

/// <summary>
/// Registers shared HTTP client defaults for service-discovered outbound clients.
/// </summary>
public static class HttpServiceCollectionExtensions
{
    /// <summary>
    /// Adds standard resilience, service discovery, and HTTP client telemetry to all configured <see cref="HttpClient" /> instances.
    /// </summary>
    /// <remarks>
    /// Automatic retries are disabled for unsafe HTTP methods. Registers built-in HTTP client instrumentation
    /// and the existing logical outcome activity source for response processing.
    /// </remarks>
    /// <param name="services">The service collection to update.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddHttpClientDefaults(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.ConfigureHttpClientDefaults(http =>
        {
            http.AddStandardResilienceHandler(options => options.Retry.DisableForUnsafeHttpMethods());
            http.AddServiceDiscovery();
        });

        services.AddOpenTelemetry()
            .WithMetrics(metrics => metrics.AddHttpClientInstrumentation())
            .WithTracing(tracing => tracing
                .AddSource(HttpOutcomeMapper.ActivitySourceName)
                .AddHttpClientInstrumentation());

        return services;
    }
}
