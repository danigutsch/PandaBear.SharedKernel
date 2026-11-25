namespace SharedKernel.AspNetCore.Authentication.Bff;

/// <summary>
/// Claim types that identify the server-side token session of a signed-in user.
/// </summary>
/// <remarks>
/// <see cref="UserTokenManager.StartSession" /> adds both claims at sign-in; they live in the authentication ticket,
/// never in a token. The session identifier is an opaque random value that keys the protected token entries.
/// </remarks>
public static class BffClaimTypes
{
    /// <summary>The opaque identifier of the token session.</summary>
    public const string SessionId = "urn:sharedkernel:bff:session-id";

    /// <summary>The absolute end of the token session, as a round-trip (ISO 8601) timestamp.</summary>
    public const string SessionExpiresAt = "urn:sharedkernel:bff:session-expires-at";
}
