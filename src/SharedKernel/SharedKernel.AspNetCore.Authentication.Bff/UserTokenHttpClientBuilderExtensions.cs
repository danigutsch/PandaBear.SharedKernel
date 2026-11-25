using Microsoft.Extensions.DependencyInjection;

namespace SharedKernel.AspNetCore.Authentication.Bff;

/// <summary>
/// Adds the user access-token and token-exchange handlers to an <see cref="HttpClient" />.
/// </summary>
public static class UserTokenHttpClientBuilderExtensions
{
    /// <summary>
    /// Attaches the current user's access token, refreshed when it nears expiry, to every request of the client.
    /// </summary>
    /// <param name="builder">The HTTP client builder.</param>
    /// <returns>The same builder.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder" /> is <see langword="null" />.</exception>
    /// <remarks>
    /// Requires <see cref="UserTokenManagementServiceCollectionExtensions.AddUserTokenManagement" />. When no token is
    /// available the request is not sent and <see cref="UserTokenUnavailableException" /> is thrown.
    /// </remarks>
    public static IHttpClientBuilder AddUserAccessTokenHandler(this IHttpClientBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.AddHttpMessageHandler(services => new UserAccessTokenHandler(
            services.GetRequiredService<ICurrentUserAccessor>(),
            services.GetRequiredService<UserTokenManager>()));
    }

    /// <summary>
    /// Exchanges the current user's access token for a token of <paramref name="audience" /> (RFC 8693) and attaches it to
    /// every request of the client. Exchanged tokens are cached per session and audience until they near expiry.
    /// </summary>
    /// <param name="builder">The HTTP client builder.</param>
    /// <param name="audience">The <c>audience</c> to request, such as the backend API's client identifier.</param>
    /// <param name="scope">The optional <c>scope</c> to request; some identity providers need it to issue the audience.</param>
    /// <returns>The same builder.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder" /> or <paramref name="audience" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="audience" /> is empty or white space, or <paramref name="scope" /> is empty or white space.</exception>
    /// <remarks>
    /// Requires <see cref="UserTokenManagementServiceCollectionExtensions.AddUserTokenManagement" />. Do not also add
    /// <see cref="AddUserAccessTokenHandler" />: this handler obtains the user's token itself. When no token is available the
    /// request is not sent and <see cref="UserTokenUnavailableException" /> is thrown.
    /// </remarks>
    public static IHttpClientBuilder AddTokenExchangeHandler(this IHttpClientBuilder builder, string audience, string? scope = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(audience);
        if (scope is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(scope);
        }

        var target = new TokenExchangeTarget(audience, scope);
        builder.Services.Configure<UserTokenManagementOptions>(options => options.ExchangeTargets.Add(target));
        return builder.AddHttpMessageHandler(services => new TokenExchangeHandler(
            services.GetRequiredService<ICurrentUserAccessor>(),
            services.GetRequiredService<UserTokenManager>(),
            target));
    }
}
