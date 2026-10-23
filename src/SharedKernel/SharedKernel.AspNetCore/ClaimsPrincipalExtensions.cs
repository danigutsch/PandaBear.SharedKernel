using System.Security.Claims;

namespace PandaBear.AspNetCore;

/// <summary>Reads provider-neutral subject identity without introducing ambient request state.</summary>
public static class ClaimsPrincipalExtensions
{
    /// <summary>Gets the unchanged JWT subject, or null when absent or blank.</summary>
    /// <param name="principal">The principal supplied by the host's authentication boundary.</param>
    /// <returns>The opaque subject identifier; display names and mapped name identifiers are never substituted.</returns>
    /// <remarks>This lookup does not authenticate a principal. Hosts must validate tokens and enforce authorization separately.</remarks>
    /// <exception cref="ArgumentNullException"><paramref name="principal"/> is null.</exception>
    public static string? GetSubjectId(this ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);
        var subject = principal.FindFirstValue(ApiAuthenticationDefaults.SubjectClaimType);
        return string.IsNullOrWhiteSpace(subject) ? null : subject;
    }
}
