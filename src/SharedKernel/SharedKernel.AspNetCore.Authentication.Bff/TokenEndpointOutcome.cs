namespace PandaBear.AspNetCore.Authentication.Bff;

/// <summary>
/// The outcome of one call to the identity provider: an answer, a rejection, or no usable answer.
/// </summary>
internal sealed class TokenEndpointOutcome
{
    private TokenEndpointOutcome(TokenEndpointAnswer? answer, bool rejected)
    {
        Answer = answer;
        Rejected = rejected;
    }

    /// <summary>Gets the answer, when the identity provider granted the request.</summary>
    internal TokenEndpointAnswer? Answer
    {
        get;
    }

    /// <summary>Gets a value indicating whether the identity provider answered with an OAuth error.</summary>
    internal bool Rejected
    {
        get;
    }

    /// <summary>Creates the outcome of a granted request.</summary>
    /// <param name="answer">The answer.</param>
    /// <returns>The outcome.</returns>
    internal static TokenEndpointOutcome Granted(TokenEndpointAnswer answer)
    {
        return new TokenEndpointOutcome(answer, rejected: false);
    }

    /// <summary>Creates the outcome of a request the identity provider refused with an OAuth error.</summary>
    /// <returns>The outcome.</returns>
    internal static TokenEndpointOutcome Rejection()
    {
        return new TokenEndpointOutcome(answer: null, rejected: true);
    }

    /// <summary>Creates the outcome of a call that got no usable answer.</summary>
    /// <returns>The outcome.</returns>
    internal static TokenEndpointOutcome Unavailable()
    {
        return new TokenEndpointOutcome(answer: null, rejected: false);
    }
}
