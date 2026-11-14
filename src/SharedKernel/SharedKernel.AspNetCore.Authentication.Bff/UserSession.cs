using System.Globalization;
using System.Security.Claims;

namespace PandaBear.AspNetCore.Authentication.Bff;

/// <summary>
/// The token session of a principal: its opaque identifier and absolute end.
/// </summary>
internal sealed class UserSession
{
    /// <summary>Initializes a new instance of the <see cref="UserSession" /> class.</summary>
    /// <param name="id">The opaque session identifier.</param>
    /// <param name="expiresAt">The absolute end of the session.</param>
    internal UserSession(string id, DateTimeOffset expiresAt)
    {
        Id = id;
        ExpiresAt = expiresAt;
    }

    /// <summary>Gets the opaque session identifier.</summary>
    internal string Id
    {
        get;
    }

    /// <summary>Gets the absolute end of the session.</summary>
    internal DateTimeOffset ExpiresAt
    {
        get;
    }

    /// <summary>Reads the session claims of an authenticated principal.</summary>
    /// <param name="user">The principal.</param>
    /// <returns>The session, or <see langword="null" /> when the principal carries no valid session.</returns>
    internal static UserSession? From(ClaimsPrincipal user)
    {
        if (user.Identity?.IsAuthenticated is not true)
        {
            return null;
        }

        var id = user.FindFirst(BffClaimTypes.SessionId)?.Value;
        var expiresAt = user.FindFirst(BffClaimTypes.SessionExpiresAt)?.Value;
        return string.IsNullOrWhiteSpace(id)
            || !DateTimeOffset.TryParseExact(expiresAt, "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var end)
            ? null
            : new UserSession(id, end);
    }

    /// <summary>Creates the claims of a new session.</summary>
    /// <param name="id">The opaque session identifier.</param>
    /// <param name="expiresAt">The absolute end of the session.</param>
    /// <returns>An identity that carries only the session claims.</returns>
    internal static ClaimsIdentity CreateIdentity(string id, DateTimeOffset expiresAt)
    {
        return new ClaimsIdentity(
        [
            new Claim(BffClaimTypes.SessionId, id),
            new Claim(BffClaimTypes.SessionExpiresAt, expiresAt.ToString("O", CultureInfo.InvariantCulture))
        ]);
    }

    /// <summary>Removes every session claim from every identity of the principal.</summary>
    /// <param name="user">The principal.</param>
    internal static void RemoveClaims(ClaimsPrincipal user)
    {
        foreach (var identity in user.Identities)
        {
            foreach (var claim in identity.Claims
                .Where(claim => claim.Type is BffClaimTypes.SessionId or BffClaimTypes.SessionExpiresAt)
                .ToArray())
            {
                identity.TryRemoveClaim(claim);
            }
        }
    }
}
