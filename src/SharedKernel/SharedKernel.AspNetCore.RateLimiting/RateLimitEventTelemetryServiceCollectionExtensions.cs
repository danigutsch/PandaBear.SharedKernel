using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace PandaBear.AspNetCore.RateLimiting;

/// <summary>
/// Registers the opt-in rate-limit rejection telemetry.
/// </summary>
public static class RateLimitEventTelemetryServiceCollectionExtensions
{
    /// <summary>
    /// Logs and counts rate-limit rejections with the policy name. An existing rejection callback keeps running afterwards.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// Not instrumented: a limiter configured only through the <c>UseRateLimiter(RateLimiterOptions)</c> overload.
    /// Reporting never throws into the host.
    /// </remarks>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddRateLimitEventTelemetry(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddMetrics();
        services.TryAddSingleton<SecurityEventReporter>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IPostConfigureOptions<RateLimiterOptions>, RateLimiterSecurityEventsPostConfigureOptions>());
        return services;
    }
}
