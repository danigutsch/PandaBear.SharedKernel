namespace SharedKernel.AspNetCore;

/// <summary>
/// Defines provider-neutral API authentication configuration and claim conventions.
/// </summary>
public static class ApiAuthenticationDefaults
{
    /// <summary>
    /// The configuration key for the OIDC discovery authority.
    /// </summary>
    public const string AuthorityConfigurationKey = "Authentication:Authority";

    /// <summary>
    /// The configuration key for the exact token issuer.
    /// </summary>
    public const string IssuerConfigurationKey = "Authentication:Issuer";

    /// <summary>
    /// The Development-only configuration key that permits an HTTP local identity provider.
    /// </summary>
    public const string AllowHttpDevelopmentAuthorityConfigurationKey = "Authentication:AllowHttpDevelopmentAuthority";

    /// <summary>
    /// The application-owned claim type that carries validated permissions.
    /// </summary>
    public const string PermissionClaimType = "permission";

    /// <summary>
    /// The provider claim type that carries source roles before local permission mapping.
    /// </summary>
    public const string RolesClaimType = "roles";
}
