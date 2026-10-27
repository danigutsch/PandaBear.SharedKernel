using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace PandaBear.AspNetCore;

/// <summary>
/// Registers the opt-in unhandled-exception handler and security-event telemetry.
/// </summary>
public static class AspNetCoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers an <see cref="IExceptionHandler" /> that answers with a RFC 9457 problem body carrying a
    /// <c>traceId</c> and no exception text, logs one stable event per exception and counts it in
    /// <see cref="AspNetCoreTelemetry.UnhandledExceptionsCounterName" />.
    /// </summary>
    /// <remarks>This also calls <c>AddProblemDetails()</c>, a host-wide registration that makes the exception and status-code middleware write problem details for every endpoint, not only for this handler. Intended for API hosts. The host must still call <c>UseExceptionHandler()</c> so the framework invokes the handler. A UI host that uses <c>UseExceptionHandler("/Error")</c> should not register it, because exception handlers run before the error page.</remarks>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddUnhandledExceptionHandler(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddMetrics();
        services.AddProblemDetails();
        services.TryAddSingleton<UnhandledExceptionReporter>();
        services.AddExceptionHandler<UnhandledExceptionHandler>();
        return services;
    }

    /// <summary>
    /// Logs and counts JWT bearer authentication failures, challenges and forbidden results with a bounded reason, and
    /// rate-limit rejections with the policy name. Existing events and rejection callbacks keep running afterwards.
    /// </summary>
    /// <remarks>
    /// Tokens, claims and exception messages are never logged or used as tag values. A request that presents an invalid
    /// token raises both <c>authentication_failed</c> and <c>challenge</c>. Not instrumented: a scheme that configures
    /// <c>EventsType</c>, an events subclass that overrides the virtual methods without calling the <c>On*</c> delegates, and a limiter
    /// configured only through the <c>UseRateLimiter(RateLimiterOptions)</c> overload. Reporting never throws into the host.
    /// </remarks>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddSecurityEventTelemetry(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddMetrics();
        services.TryAddSingleton<SecurityEventReporter>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IPostConfigureOptions<JwtBearerOptions>, JwtBearerSecurityEventsPostConfigureOptions>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IPostConfigureOptions<RateLimiterOptions>, RateLimiterSecurityEventsPostConfigureOptions>());
        return services;
    }
}
