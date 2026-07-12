using System.Security.Claims;
using System.Reflection;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;

namespace SharedKernel.AspNetCore;

/// <summary>
/// Configures a fail-closed JWT bearer authentication boundary for an API.
/// </summary>
public static class ApiAuthenticationServiceCollectionExtensions
{
    private const string OpenApiDocumentGenerationEntryAssemblyName = "GetDocument.Insider";
    private const string OpenApiDocumentGenerationAuthority = "https://openapi.invalid";

    /// <summary>
    /// Adds bearer authentication, permission-claim transformation, and an authenticated fallback policy.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="environment">The host environment.</param>
    /// <param name="audience">The only audience accepted by this API boundary.</param>
    /// <param name="permissionsByRole">The application-owned permissions granted for each validated role.</param>
    /// <returns>An authorization builder for boundary-specific permission policies.</returns>
    public static AuthorizationBuilder AddApiBearerAuthentication(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment,
        string audience,
        IReadOnlyDictionary<string, IReadOnlyCollection<string>> permissionsByRole)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentException.ThrowIfNullOrWhiteSpace(audience);
        ArgumentNullException.ThrowIfNull(permissionsByRole);

        var authority = configuration[ApiAuthenticationDefaults.AuthorityConfigurationKey];
        var issuer = configuration[ApiAuthenticationDefaults.IssuerConfigurationKey];
        if (Assembly.GetEntryAssembly()?.GetName().Name == OpenApiDocumentGenerationEntryAssemblyName)
        {
            authority ??= OpenApiDocumentGenerationAuthority;
            issuer ??= OpenApiDocumentGenerationAuthority;
        }

        var allowHttpDevelopmentAuthority = environment.IsDevelopment()
            && string.Equals(
                configuration[ApiAuthenticationDefaults.AllowHttpDevelopmentAuthorityConfigurationKey],
                bool.TrueString,
                StringComparison.OrdinalIgnoreCase);

        ValidateConfiguration(authority, issuer, allowHttpDevelopmentAuthority, environment);

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                if (!string.IsNullOrWhiteSpace(authority))
                {
                    options.Authority = authority;
                    options.RequireHttpsMetadata = !allowHttpDevelopmentAuthority;
                }

                options.Audience = audience;
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = issuer,
                    ValidateAudience = true,
                    ValidAudience = audience,
                    ValidateIssuerSigningKey = true,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(2),
                    ValidAlgorithms = [SecurityAlgorithms.RsaSha256]
                };
            });

        services.AddTransient<IClaimsTransformation>(_ => new PermissionClaimsTransformation(permissionsByRole));

        return services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build());
    }

    private static void ValidateConfiguration(
        string? authority,
        string? issuer,
        bool allowHttpDevelopmentAuthority,
        IHostEnvironment environment)
    {
        if (string.IsNullOrWhiteSpace(authority) || string.IsNullOrWhiteSpace(issuer))
        {
            if (!environment.IsDevelopment())
            {
                throw new InvalidOperationException($"{ApiAuthenticationDefaults.AuthorityConfigurationKey} and {ApiAuthenticationDefaults.IssuerConfigurationKey} must be configured outside Development.");
            }

            return;
        }

        if (!Uri.TryCreate(authority, UriKind.Absolute, out var authorityUri)
            || (!string.Equals(authorityUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
                && !allowHttpDevelopmentAuthority))
        {
            throw new InvalidOperationException($"{ApiAuthenticationDefaults.AuthorityConfigurationKey} must be an HTTPS absolute URI outside an explicitly configured Development environment.");
        }

        if (!Uri.TryCreate(issuer, UriKind.Absolute, out var issuerUri)
            || (!string.Equals(issuerUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
                && !allowHttpDevelopmentAuthority))
        {
            throw new InvalidOperationException($"{ApiAuthenticationDefaults.IssuerConfigurationKey} must be an HTTPS absolute URI outside an explicitly configured Development environment.");
        }
    }

    private sealed class PermissionClaimsTransformation(IReadOnlyDictionary<string, IReadOnlyCollection<string>> permissionsByRole)
        : IClaimsTransformation
    {
        public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
        {
            ArgumentNullException.ThrowIfNull(principal);

            var permissions = principal.FindAll(ApiAuthenticationDefaults.RolesClaimType)
                .Where(role => permissionsByRole.TryGetValue(role.Value, out _))
                .SelectMany(role => permissionsByRole[role.Value])
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            if (permissions.Length == 0)
            {
                return Task.FromResult(principal);
            }

            if (principal.Identity is not ClaimsIdentity identity)
            {
                return Task.FromResult(principal);
            }

            foreach (var permission in permissions.Where(permission => !principal.HasClaim(ApiAuthenticationDefaults.PermissionClaimType, permission)))
            {
                identity.AddClaim(new Claim(ApiAuthenticationDefaults.PermissionClaimType, permission));
            }

            return Task.FromResult(principal);
        }
    }
}
