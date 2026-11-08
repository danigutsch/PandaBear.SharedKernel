using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace PandaBear.AspNetCore.Authentication;

/// <summary>
/// Registers the opt-in authentication-event telemetry.
/// </summary>
public static class AuthenticationEventTelemetryServiceCollectionExtensions
{
    /// <summary>
    /// Logs and counts JWT bearer authentication failures, challenges and forbidden results with a bounded reason.
    /// Existing events keep running afterwards.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// Tokens, claims and exception messages are never logged or used as tag values. A request that presents an invalid
    /// token raises both <c>authentication_failed</c> and <c>challenge</c>. Not instrumented: a scheme that configures
    /// <c>EventsType</c>, and an events subclass that overrides the virtual methods without calling the <c>On*</c> delegates.
    /// Reporting never throws into the host.
    /// </remarks>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddAuthenticationEventTelemetry(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddMetrics();
        services.TryAddSingleton<SecurityEventReporter>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IPostConfigureOptions<JwtBearerOptions>, JwtBearerSecurityEventsPostConfigureOptions>());
        return services;
    }
}
