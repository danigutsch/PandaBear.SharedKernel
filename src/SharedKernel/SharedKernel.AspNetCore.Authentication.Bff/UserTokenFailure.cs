namespace SharedKernel.AspNetCore.Authentication.Bff;

/// <summary>
/// Why a user or audience access token is unavailable.
/// </summary>
public enum UserTokenFailure
{
    /// <summary>The token is available.</summary>
    None = 0,

    /// <summary>The user is not signed in, or the principal carries no token session.</summary>
    NoSession = 1,

    /// <summary>The token session reached its absolute end.</summary>
    SessionExpired = 2,

    /// <summary>The session has no stored tokens, for example after sign-out or a rejected refresh.</summary>
    NoTokens = 3,

    /// <summary>
    /// The access token expired and could not be renewed: there is no refresh token or the identity provider rejected it.
    /// The token session is ended; the user must sign in again.
    /// </summary>
    RefreshFailed = 4,

    /// <summary>The identity provider rejected the token exchange or answered with an unusable token.</summary>
    ExchangeFailed = 5,

    /// <summary>
    /// The identity provider did not answer in time, failed, or sent an unreadable answer. The stored tokens are kept, so
    /// a later call can succeed.
    /// </summary>
    IdentityProviderUnavailable = 6
}
