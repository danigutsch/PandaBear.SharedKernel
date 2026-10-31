using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace PandaBear.Blazor;

/// <summary>Registers the Blazor helper services.</summary>
public static class BlazorServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="UiFailureReporter" /> as the <see cref="IUiFailureReporter" />, unless one is registered.
    /// Also calls <c>AddMetrics()</c> so the <see cref="System.Diagnostics.Metrics.IMeterFactory" /> the reporter needs is available;
    /// that call is idempotent, so a factory the host or caller already registered is kept. Register the
    /// <see cref="UiFailureReporter.MeterName" /> meter with OpenTelemetry to export the counter.
    /// </summary>
    /// <param name="services">The service collection to update.</param>
    /// <returns>The same service collection.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is <see langword="null"/>.</exception>
    public static IServiceCollection AddUiFailureReporting(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddMetrics();
        services.TryAddSingleton<IUiFailureReporter, UiFailureReporter>();
        return services;
    }
}
