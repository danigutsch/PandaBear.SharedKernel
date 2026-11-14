namespace PandaBear.AspNetCore.Authentication.Bff;

/// <summary>
/// The outcome of one call to the identity provider: an answer, a rejection, or no usable answer.
/// </summary>
internal sealed class TokenEndpointOutcome
{
    private TokenEndpointOutcome(TokenEndpointAnswer? answer, string? error)
    {
        Answer = answer;
        Error = error;
    }

    /// <summary>Gets the answer, when the identity provider granted the request.</summary>
    internal TokenEndpointAnswer? Answer { get; }

    /// <summary>Gets the OAuth error code the identity provider refused the request with, if it did.</summary>
    internal string? Error { get; }

    /// <summary>Gets a value indicating whether the identity provider answered with an OAuth error.</summary>
    internal bool Rejected => Error is not null;

    /// <summary>
    /// Gets a value indicating whether the identity provider refused the grant itself (<c>invalid_grant</c>, RFC 6749
    /// section 5.2), so the refresh token can never be redeemed. Other errors concern the client or the request.
    /// </summary>
    internal bool GrantRejected => string.Equals(Error, "invalid_grant", StringComparison.Ordinal);

    /// <summary>Creates the outcome of a granted request.</summary>
    /// <param name="answer">The answer.</param>
    /// <returns>The outcome.</returns>
    internal static TokenEndpointOutcome Granted(TokenEndpointAnswer answer)
    {
        return new TokenEndpointOutcome(answer, error: null);
    }

    /// <summary>Creates the outcome of a request the identity provider refused with an OAuth error.</summary>
    /// <param name="error">The OAuth error code.</param>
    /// <returns>The outcome.</returns>
    internal static TokenEndpointOutcome Rejection(string error)
    {
        return new TokenEndpointOutcome(answer: null, error);
    }

    /// <summary>Creates the outcome of a call that got no usable answer.</summary>
    /// <returns>The outcome.</returns>
    internal static TokenEndpointOutcome Unavailable()
    {
        return new TokenEndpointOutcome(answer: null, error: null);
    }
}
