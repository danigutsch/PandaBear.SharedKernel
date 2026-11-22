using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace PandaBear.AspNetCore.Authentication.Bff;

/// <summary>
/// Registers server-side user token management.
/// </summary>
public static class UserTokenManagementServiceCollectionExtensions
{
    private const long MaximumIdentityProviderAnswerBytes = 256 * 1024;

    /// <summary>
    /// Registers <see cref="UserTokenManager" /> and hooks it into the OpenID Connect and cookie schemes: a validated login
    /// starts a token session, cookie sign-out ends it and revokes the refresh token, and a cookie whose token session has
    /// ended is rejected.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <param name="configure">Configures the options.</param>
    /// <returns>The same services.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services" /> is <see langword="null" />.</exception>
    /// <remarks>
    /// The application registers the <see cref="Microsoft.Extensions.Caching.Distributed.IDistributedCache" />, persists and
    /// shares Data Protection keys across instances, and configures the OpenID Connect scheme with its client identifier and
    /// secret. Register an <see cref="IUserTokenCacheConcurrency" /> singleton for atomic conditional token mutations.
    /// Neither scheme may set <c>EventsType</c>; assign an events instance to <c>Events</c> instead.
    /// </remarks>
    public static IServiceCollection AddUserTokenManagement(
        this IServiceCollection services,
        Action<UserTokenManagementOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = services.AddOptions<UserTokenManagementOptions>()
            .Validate(value => !string.IsNullOrWhiteSpace(value.OpenIdConnectScheme), "OpenIdConnectScheme must be set.")
            .Validate(value => !string.IsNullOrWhiteSpace(value.CookieScheme), "CookieScheme must be set.")
            .Validate(value => !string.IsNullOrWhiteSpace(value.CacheKeyPrefix), "CacheKeyPrefix must be set.")
            .Validate(value => value.SessionLifetime > TimeSpan.Zero, "SessionLifetime must be positive.")
            .Validate(value => value.RefreshBeforeExpiry >= TimeSpan.Zero, "RefreshBeforeExpiry must not be negative.")
            .Validate(value => value.DefaultAccessTokenLifetime > TimeSpan.Zero, "DefaultAccessTokenLifetime must be positive.")
            .Validate(value => value.IdentityProviderTimeout > TimeSpan.Zero, "IdentityProviderTimeout must be positive.");
        if (configure is not null)
        {
            options.Configure(configure);
        }

        services.TryAddSingleton(TimeProvider.System);
        services.AddHttpContextAccessor();
        services.TryAddSingleton<IUserSessionLock, InProcessUserSessionLock>();
        services.TryAddSingleton<ICurrentUserAccessor, HttpContextCurrentUserAccessor>();
        services.TryAddSingleton<UserTokenManager>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IPostConfigureOptions<OpenIdConnectOptions>, TokenSessionPostConfigureOptions>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IPostConfigureOptions<CookieAuthenticationOptions>, TokenSessionPostConfigureOptions>());
        services.AddHttpClient(UserTokenManagementDefaults.IdentityProviderHttpClientName)
            .ConfigureHttpClient((provider, client) =>
            {
                client.Timeout = provider.GetRequiredService<IOptions<UserTokenManagementOptions>>().Value.IdentityProviderTimeout;
                client.MaxResponseContentBufferSize = MaximumIdentityProviderAnswerBytes;
            })
            .ConfigurePrimaryHttpMessageHandler(static () => new SocketsHttpHandler { AllowAutoRedirect = false })
            .ConfigureAdditionalHttpMessageHandlers(static (handlers, _) => handlers.Clear());
        return services;
    }

    /// <summary>
    /// Makes the token handlers find the user of the running Blazor Server circuit, where no HTTP request is current, and
    /// otherwise the user of the HTTP request.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <returns>The same services.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services" /> is <see langword="null" />.</exception>
    public static IServiceCollection AddBlazorServerCurrentUser(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddHttpContextAccessor();
        services.Replace(ServiceDescriptor.Singleton<ICurrentUserAccessor, CircuitCurrentUserAccessor>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<CircuitHandler, ServicesAccessorCircuitHandler>());
        return services;
    }
}
