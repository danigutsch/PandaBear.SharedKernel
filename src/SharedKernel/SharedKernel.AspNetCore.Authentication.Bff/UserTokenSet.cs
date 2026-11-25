namespace SharedKernel.AspNetCore.Authentication.Bff;

/// <summary>
/// The tokens of one signed-in session, as issued by the identity provider.
/// </summary>
/// <remarks><see cref="ToString" /> never includes a token, so an instance is safe to log by accident.</remarks>
public sealed class UserTokenSet
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UserTokenSet" /> class.
    /// </summary>
    /// <param name="accessToken">The access token.</param>
    /// <param name="accessTokenExpiresAt">When the access token expires.</param>
    /// <param name="refreshToken">The refresh token, if the identity provider issued one.</param>
    /// <param name="identityToken">The ID token, if any; it can serve as <c>id_token_hint</c> at the end-session endpoint.</param>
    /// <param name="accessTokenType">The token type, such as <c>Bearer</c>, if known.</param>
    /// <param name="scope">The granted scope, if the identity provider returned it.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="accessToken" /> is empty or white space.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="accessToken" /> is <see langword="null" />.</exception>
    public UserTokenSet(
        string accessToken,
        DateTimeOffset accessTokenExpiresAt,
        string? refreshToken,
        string? identityToken,
        string? accessTokenType,
        string? scope)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);

        AccessToken = accessToken;
        AccessTokenExpiresAt = accessTokenExpiresAt;
        RefreshToken = string.IsNullOrWhiteSpace(refreshToken) ? null : refreshToken;
        IdentityToken = string.IsNullOrWhiteSpace(identityToken) ? null : identityToken;
        AccessTokenType = string.IsNullOrWhiteSpace(accessTokenType) ? null : accessTokenType;
        Scope = string.IsNullOrWhiteSpace(scope) ? null : scope;
    }

    /// <summary>Gets the access token.</summary>
    public string AccessToken { get; }

    /// <summary>Gets when the access token expires.</summary>
    public DateTimeOffset AccessTokenExpiresAt { get; }

    /// <summary>Gets the refresh token, if any.</summary>
    public string? RefreshToken { get; }

    /// <summary>Gets the ID token, if any.</summary>
    public string? IdentityToken { get; }

    /// <summary>Gets the access token type, if known.</summary>
    public string? AccessTokenType { get; }

    /// <summary>Gets the granted scope, if known.</summary>
    public string? Scope { get; }

    /// <summary>Returns a description without any token.</summary>
    /// <returns>A redacted description.</returns>
    public override string ToString()
    {
        return $"UserTokenSet {{ AccessTokenExpiresAt = {AccessTokenExpiresAt:O}, HasRefreshToken = {RefreshToken is not null}, tokens redacted }}";
    }
}
