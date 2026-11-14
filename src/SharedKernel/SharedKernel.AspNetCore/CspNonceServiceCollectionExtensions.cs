using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace PandaBear.AspNetCore;

/// <summary>Registers the per-request Content-Security-Policy nonce.</summary>
public static class CspNonceServiceCollectionExtensions
{
    /// <summary>Registers <see cref="ICspNonce" /> as a scoped service, one value per request.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same service collection.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is <see langword="null"/>.</exception>
    public static IServiceCollection AddCspNonce(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddScoped<ICspNonce, CspNonce>();
        return services;
    }
}
