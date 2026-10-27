using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace PandaBear.Blazor;

/// <summary>Registers the Blazor helper services.</summary>
public static class BlazorServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="UiFailureReporter" /> as the <see cref="IUiFailureReporter" />, unless one is registered.
    /// Register an <see cref="IHandledFailureCounter" /> to also count handled failures. Without one, failures are
    /// logged and not counted; no warning is raised for the missing counter. The reporter is a singleton, so the
    /// counter must be registered as a singleton or be safe to use from one.
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
