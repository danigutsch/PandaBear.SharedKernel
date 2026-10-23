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
    public static IServiceCollection AddHttpClientDefaults(this IServiceCollection services) =>
        services.AddHttpClientDefaults(configureResilience: null);

    /// <summary>
    /// Adds shared HTTP defaults with application-owned customization of the standard resilience options.
    /// </summary>
    /// <remarks>
    /// The callback configures shared standard pipeline options, not an individual named client.
    /// Scoped response policies must inspect the current request in their resilience predicates rather
    /// than capture an ambient HTTP request during registration. No additional pipeline is installed.
    /// Unsafe-method retries are disabled after the callback, including when it changes the retry
    /// predicate. Service discovery and HTTP client telemetry remain enabled.
    /// </remarks>
    /// <param name="services">The service collection to update.</param>
    /// <param name="configureResilience">Optional shared options customization; null preserves the standard defaults.</param>
    /// <returns>The same service collection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
    public static IServiceCollection AddHttpClientDefaults(
        this IServiceCollection services,
        Action<HttpStandardResilienceOptions>? configureResilience)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.ConfigureHttpClientDefaults(http =>
        {
            http.AddStandardResilienceHandler(options =>
            {
                configureResilience?.Invoke(options);
                options.Retry.DisableForUnsafeHttpMethods();
            });
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
