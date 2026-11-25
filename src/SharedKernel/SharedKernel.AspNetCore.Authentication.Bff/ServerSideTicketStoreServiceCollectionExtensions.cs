using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace SharedKernel.AspNetCore.Authentication.Bff;

/// <summary>
/// Registers the server-side cookie ticket store.
/// </summary>
public static class ServerSideTicketStoreServiceCollectionExtensions
{
    /// <summary>
    /// Keeps the tickets of a cookie scheme in the distributed cache, protected with Data Protection; the browser cookie then
    /// carries only an opaque key.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <param name="cookieScheme">The cookie scheme whose session store is set.</param>
    /// <param name="configure">Configures the options.</param>
    /// <returns>The same services.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services" /> or <paramref name="cookieScheme" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="cookieScheme" /> is empty or white space.</exception>
    /// <remarks>
    /// The application registers the <see cref="Microsoft.Extensions.Caching.Distributed.IDistributedCache" /> and persists and
    /// shares Data Protection keys across instances.
    /// </remarks>
    public static IServiceCollection AddServerSideTicketStore(
        this IServiceCollection services,
        string cookieScheme = CookieAuthenticationDefaults.AuthenticationScheme,
        Action<ServerSideTicketStoreOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(cookieScheme);

        var options = services.AddOptions<ServerSideTicketStoreOptions>();
        if (configure is not null)
        {
            options.Configure(configure);
        }

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<ServerSideTicketStore>();
        services.AddOptions<CookieAuthenticationOptions>(cookieScheme)
            .Configure<ServerSideTicketStore>(static (cookie, store) => cookie.SessionStore = store);
        return services;
    }
}
