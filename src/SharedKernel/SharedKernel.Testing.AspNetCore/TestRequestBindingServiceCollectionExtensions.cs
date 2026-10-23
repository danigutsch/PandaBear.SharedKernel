using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace PandaBear.Testing.AspNetCore;

/// <summary>Configures deterministic request-binding failures in ASP.NET Core test hosts.</summary>
public static class TestRequestBindingServiceCollectionExtensions
{
    /// <summary>Returns HTTP 400 for invalid request bodies without enabling Development-only binding exceptions.</summary>
    /// <param name="services">The test host service collection.</param>
    /// <returns>The same service collection for further test configuration.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is null.</exception>
    public static IServiceCollection AddTestRequestBindingDefaults(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = false);
        return services;
    }
}
