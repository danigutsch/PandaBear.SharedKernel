using Microsoft.AspNetCore.Authorization;

namespace PandaBear.AspNetCore;

/// <summary>
/// Provides authorization policy helpers for application-owned permissions.
/// </summary>
public static class AuthorizationPolicyBuilderExtensions
{
    /// <summary>
    /// Requires the authenticated principal to have an application-owned permission claim.
    /// </summary>
    /// <param name="builder">The policy builder.</param>
    /// <param name="permission">The required permission value.</param>
    /// <returns>The configured policy builder.</returns>
    /// <exception cref="ArgumentException">When <paramref name="permission"/> is empty or white space.</exception>
    /// <exception cref="ArgumentNullException">When <paramref name="builder"/> or <paramref name="permission"/> is <see langword="null"/>.</exception>
    public static AuthorizationPolicyBuilder RequirePermission(this AuthorizationPolicyBuilder builder, string permission)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(permission);

        return builder.RequireClaim(ApiAuthenticationDefaults.PermissionClaimType, permission);
    }
}
