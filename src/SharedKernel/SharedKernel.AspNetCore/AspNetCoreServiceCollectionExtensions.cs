using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace PandaBear.AspNetCore;

/// <summary>
/// Registers the opt-in unhandled-exception handler.
/// </summary>
public static class AspNetCoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers an <see cref="IExceptionHandler" /> that answers with a RFC 9457 problem body carrying a
    /// <c>traceId</c> and no exception text, logs one stable event per exception and counts it in
    /// <see cref="AspNetCoreTelemetry.UnhandledExceptionsCounterName" />.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is <see langword="null"/>.</exception>
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
}
