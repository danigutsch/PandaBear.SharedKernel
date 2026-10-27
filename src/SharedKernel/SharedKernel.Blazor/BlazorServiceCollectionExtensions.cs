using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace PandaBear.Blazor;

/// <summary>Registers the Blazor helper services.</summary>
public static class BlazorServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="UiFailureReporter" /> as the <see cref="IUiFailureReporter" />, unless one is registered.
    /// Requires an <see cref="System.Diagnostics.Metrics.IMeterFactory" />, which <c>AddMetrics()</c> and the ASP.NET Core host
    /// register; register the <see cref="UiFailureReporter.MeterName" /> meter with OpenTelemetry to export the counter.
    /// </summary>
    /// <param name="services">The service collection to update.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddUiFailureReporting(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IUiFailureReporter, UiFailureReporter>();
        return services;
    }
}
