namespace SharedKernel.AspNetCore.Authentication.Bff;

/// <summary>
/// Thrown by the access-token and token-exchange <see cref="DelegatingHandler" />s when no token can be attached; the
/// request is not sent.
/// </summary>
/// <remarks>
/// Catch it where the UI can react, for example by sending the user to sign in again when <see cref="Failure" /> is
/// <see cref="UserTokenFailure.RefreshFailed" />, <see cref="UserTokenFailure.NoTokens" /> or
/// <see cref="UserTokenFailure.SessionExpired" />.
/// </remarks>
public sealed class UserTokenUnavailableException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="UserTokenUnavailableException" /> class.</summary>
    public UserTokenUnavailableException()
        : this(UserTokenFailure.NoSession)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="UserTokenUnavailableException" /> class.</summary>
    /// <param name="message">The message.</param>
    public UserTokenUnavailableException(string message)
        : base(message)
    {
        Failure = UserTokenFailure.NoSession;
    }

    /// <summary>Initializes a new instance of the <see cref="UserTokenUnavailableException" /> class.</summary>
    /// <param name="message">The message.</param>
    /// <param name="innerException">The cause.</param>
    public UserTokenUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
        Failure = UserTokenFailure.NoSession;
    }

    /// <summary>Initializes a new instance of the <see cref="UserTokenUnavailableException" /> class.</summary>
    /// <param name="failure">Why the token is unavailable.</param>
    public UserTokenUnavailableException(UserTokenFailure failure)
        : base($"No access token is available: {failure}.")
    {
        Failure = failure;
    }

    /// <summary>Gets why the token is unavailable.</summary>
    public UserTokenFailure Failure { get; }
}
