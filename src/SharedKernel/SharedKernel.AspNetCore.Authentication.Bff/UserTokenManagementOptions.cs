using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;

namespace SharedKernel.AspNetCore.Authentication.Bff;

/// <summary>
/// Options for server-side user token management.
/// </summary>
public sealed class UserTokenManagementOptions
{
    /// <summary>
    /// Gets or sets the OpenID Connect scheme whose client credentials and discovery document are used.
    /// </summary>
    public string OpenIdConnectScheme { get; set; } = OpenIdConnectDefaults.AuthenticationScheme;

    /// <summary>
    /// Gets or sets the cookie scheme whose sign-out ends the token session.
    /// </summary>
    public string CookieScheme { get; set; } = CookieAuthenticationDefaults.AuthenticationScheme;

    /// <summary>
    /// Gets or sets the absolute lifetime of a token session, counted from sign-in. Defaults to eight hours.
    /// </summary>
    /// <remarks>Keep it no longer than the cookie's absolute lifetime, such as <see cref="ServerSideTicketStoreOptions.AbsoluteLifetime" />.</remarks>
    public TimeSpan SessionLifetime { get; set; } = TimeSpan.FromHours(8);

    /// <summary>
    /// Gets or sets how long before its expiry a token is treated as expired. Defaults to one minute.
    /// </summary>
    /// <remarks>
    /// The margin absorbs clock skew between this host, the identity provider and the APIs, and the time a request spends in
    /// flight. It applies to user access tokens and to exchanged tokens.
    /// </remarks>
    public TimeSpan RefreshBeforeExpiry { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Gets or sets the lifetime assumed for an access token whose token response omits <c>expires_in</c>, which RFC 6749
    /// section 5.1 only recommends. Defaults to five minutes.
    /// </summary>
    public TimeSpan DefaultAccessTokenLifetime { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets the timeout of each call to the identity provider. Defaults to ten seconds.
    /// </summary>
    public TimeSpan IdentityProviderTimeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Gets or sets how the client authenticates to the token and revocation endpoints. Defaults to
    /// <see cref="TokenEndpointClientAuthentication.PostBody" />.
    /// </summary>
    public TokenEndpointClientAuthentication ClientAuthentication { get; set; } = TokenEndpointClientAuthentication.PostBody;

    /// <summary>
    /// Gets or sets the prefix of every distributed-cache key this package writes. Defaults to <c>bff:</c>.
    /// </summary>
    /// <remarks>Give each application its own prefix when applications share a cache.</remarks>
    public string CacheKeyPrefix { get; set; } = "bff:";

    /// <summary>
    /// Gets or sets whether sign-out revokes the refresh token at the identity provider's revocation endpoint (RFC 7009)
    /// when its discovery document names one. Defaults to <see langword="true" />.
    /// </summary>
    public bool RevokeRefreshTokenOnSignOut { get; set; } = true;

    /// <summary>
    /// Gets or sets whether the cookie handler rejects, and signs out, a principal whose token session has ended or lost its
    /// tokens, for example after a rejected refresh. Defaults to <see langword="true" />.
    /// </summary>
    public bool RejectCookieWithoutTokenSession { get; set; } = true;

    /// <summary>
    /// Gets the token-exchange targets registered by <see cref="UserTokenHttpClientBuilderExtensions.AddTokenExchangeHandler" />;
    /// sign-out removes their cached tokens.
    /// </summary>
    internal HashSet<TokenExchangeTarget> ExchangeTargets { get; } = [];
}
