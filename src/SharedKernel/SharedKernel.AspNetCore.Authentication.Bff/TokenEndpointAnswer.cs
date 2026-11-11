namespace PandaBear.AspNetCore.Authentication.Bff;

/// <summary>
/// The fields of a successful token-endpoint answer (RFC 6749 section 5.1, RFC 8693 section 2.2.1).
/// </summary>
internal sealed class TokenEndpointAnswer
{
    /// <summary>Gets the <c>access_token</c> field, if present.</summary>
    internal string? AccessToken
    {
        get; init;
    }

    /// <summary>Gets the <c>token_type</c> field, if present.</summary>
    internal string? TokenType
    {
        get; init;
    }

    /// <summary>Gets the <c>expires_in</c> field, in seconds, if present.</summary>
    internal long? ExpiresIn
    {
        get; init;
    }

    /// <summary>Gets the <c>refresh_token</c> field, if present.</summary>
    internal string? RefreshToken
    {
        get; init;
    }

    /// <summary>Gets the <c>id_token</c> field, if present.</summary>
    internal string? IdentityToken
    {
        get; init;
    }

    /// <summary>Gets the <c>scope</c> field, if present.</summary>
    internal string? Scope
    {
        get; init;
    }

    /// <summary>Gets the <c>issued_token_type</c> field, if present.</summary>
    internal string? IssuedTokenType
    {
        get; init;
    }

    /// <summary>Returns a description without any token.</summary>
    /// <returns>A redacted description.</returns>
    public override string ToString()
    {
        return "TokenEndpointAnswer { tokens redacted }";
    }
}
