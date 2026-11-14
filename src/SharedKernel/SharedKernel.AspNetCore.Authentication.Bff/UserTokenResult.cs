using System.Diagnostics.CodeAnalysis;

namespace PandaBear.AspNetCore.Authentication.Bff;

/// <summary>
/// The outcome of asking for a user or audience access token: the token, or why it is unavailable.
/// </summary>
/// <remarks><see cref="ToString" /> never includes the token.</remarks>
public sealed class UserTokenResult
{
    private UserTokenResult(string? accessToken, DateTimeOffset? expiresAt, UserTokenFailure failure)
    {
        AccessToken = accessToken;
        ExpiresAt = expiresAt;
        Failure = failure;
    }

    /// <summary>Gets a value indicating whether a token is available.</summary>
    [MemberNotNullWhen(true, nameof(AccessToken))]
    [MemberNotNullWhen(true, nameof(ExpiresAt))]
    public bool Succeeded => Failure == UserTokenFailure.None;

    /// <summary>Gets the access token, or <see langword="null" /> when <see cref="Succeeded" /> is false.</summary>
    public string? AccessToken { get; }

    /// <summary>Gets when the access token expires, or <see langword="null" /> when <see cref="Succeeded" /> is false.</summary>
    public DateTimeOffset? ExpiresAt { get; }

    /// <summary>Gets why the token is unavailable, or <see cref="UserTokenFailure.None" />.</summary>
    public UserTokenFailure Failure { get; }

    /// <summary>Returns a description without the token.</summary>
    /// <returns>A redacted description.</returns>
    public override string ToString()
    {
        return Succeeded
            ? $"UserTokenResult {{ Succeeded, ExpiresAt = {ExpiresAt:O}, token redacted }}"
            : $"UserTokenResult {{ Failure = {Failure} }}";
    }

    /// <summary>Creates a successful result.</summary>
    /// <param name="accessToken">The access token.</param>
    /// <param name="expiresAt">When the access token expires.</param>
    /// <returns>The result.</returns>
    internal static UserTokenResult Success(string accessToken, DateTimeOffset expiresAt)
    {
        return new UserTokenResult(accessToken, expiresAt, UserTokenFailure.None);
    }

    /// <summary>Creates a failed result.</summary>
    /// <param name="failure">Why the token is unavailable.</param>
    /// <returns>The result.</returns>
    internal static UserTokenResult Failed(UserTokenFailure failure)
    {
        return new UserTokenResult(accessToken: null, expiresAt: null, failure);
    }
}
