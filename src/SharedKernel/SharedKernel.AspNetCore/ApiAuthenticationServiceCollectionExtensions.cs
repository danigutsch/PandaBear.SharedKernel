using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SharedKernel.OpenApi;

namespace SharedKernel.AspNetCore;

/// <summary>
/// Configures a fail-closed JWT bearer authentication boundary for an API.
/// </summary>
public static class ApiAuthenticationServiceCollectionExtensions
{
    /// <summary>
    /// Adds the complete bearer-authentication boundary or trusted OpenAPI generation authorization services.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="environment">The host environment.</param>
    /// <param name="audience">The only audience accepted by this API boundary when bearer authentication is registered.</param>
    /// <param name="permissionsByRole">The application-owned permissions granted for each validated role.</param>
    /// <returns>An authorization builder for boundary-specific permission policies.</returns>
    public static AuthorizationBuilder AddApiSecurity(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment,
        string audience,
        IReadOnlyDictionary<string, IReadOnlyCollection<string>> permissionsByRole)
    {
        return AddApiSecurity(
            services,
            configuration,
            environment,
            audience,
            permissionsByRole,
            registerBearerAuthentication: null);
    }

    internal static AuthorizationBuilder AddApiSecurity(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment,
        string audience,
        IReadOnlyDictionary<string, IReadOnlyCollection<string>> permissionsByRole,
        bool? registerBearerAuthentication)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentException.ThrowIfNullOrWhiteSpace(audience);
        ArgumentNullException.ThrowIfNull(permissionsByRole);

        var shouldRegisterBearerAuthentication = registerBearerAuthentication
            ?? !OpenApiGenerationMode.IsEnabled(environment);

        return shouldRegisterBearerAuthentication
            ? services.AddApiBearerAuthentication(configuration, environment, audience, permissionsByRole)
            : services.AddApiAuthorization(permissionsByRole);
    }

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

        var allowHttpDevelopmentAuthority = environment.IsDevelopment()
            && string.Equals(
                configuration[ApiAuthenticationDefaults.AllowHttpDevelopmentAuthorityConfigurationKey],
                bool.TrueString,
                StringComparison.OrdinalIgnoreCase);

        ValidateConfiguration(authority, issuer, allowHttpDevelopmentAuthority);

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IPostConfigureOptions<JwtBearerOptions>, JwtBearerPostConfigureOptions>());

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddScheme<JwtBearerOptions, JwtBearerHandler>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                ConfigureBearerOptions(
                    options,
                    audience,
                    authority,
                    issuer,
                    allowHttpDevelopmentAuthority);
            });

        services.AddTransient<IClaimsTransformation>(_ => new PermissionClaimsTransformation(permissionsByRole));

        return services.AddApiAuthorization(permissionsByRole);
    }

    /// <summary>
    /// Adds authorization policies and an authenticated fallback policy without registering an authentication handler.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="permissionsByRole">The application-owned permissions granted for each validated role.</param>
    /// <returns>An authorization builder for boundary-specific permission policies.</returns>
    public static AuthorizationBuilder AddApiAuthorization(
        this IServiceCollection services,
        IReadOnlyDictionary<string, IReadOnlyCollection<string>> permissionsByRole)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(permissionsByRole);

        return services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build());
    }

    internal static void ConfigureBearerOptions(
        JwtBearerOptions options,
        string audience,
        string? authority,
        string? issuer,
        bool allowHttpDevelopmentAuthority)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(audience);

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
    }

    private static void ValidateConfiguration(
        string? authority,
        string? issuer,
        bool allowHttpDevelopmentAuthority)
    {
        if (string.IsNullOrWhiteSpace(authority) || string.IsNullOrWhiteSpace(issuer))
        {
            throw new InvalidOperationException($"{ApiAuthenticationDefaults.AuthorityConfigurationKey} and {ApiAuthenticationDefaults.IssuerConfigurationKey} must be configured.");
        }

        if (!IsAllowedAuthorityScheme(authority, allowHttpDevelopmentAuthority))
        {
            throw new InvalidOperationException($"{ApiAuthenticationDefaults.AuthorityConfigurationKey} must be an HTTPS absolute URI outside an explicitly configured Development environment.");
        }

        if (!IsAllowedAuthorityScheme(issuer, allowHttpDevelopmentAuthority))
        {
            throw new InvalidOperationException($"{ApiAuthenticationDefaults.IssuerConfigurationKey} must be an HTTPS absolute URI outside an explicitly configured Development environment.");
        }
    }

    private static bool IsAllowedAuthorityScheme(string value, bool allowHttpDevelopmentAuthority)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            return false;
        }

        return string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
               || (allowHttpDevelopmentAuthority
                   && string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase));
    }

    private sealed class PermissionClaimsTransformation(IReadOnlyDictionary<string, IReadOnlyCollection<string>> permissionsByRole)
        : IClaimsTransformation
    {
        public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
        {
            ArgumentNullException.ThrowIfNull(principal);

            RemoveProviderPermissions(principal);

            var permissions = principal.FindAll(ApiAuthenticationDefaults.RolesClaimType)
                .Where(role => permissionsByRole.TryGetValue(role.Value, out _))
                .SelectMany(role => permissionsByRole[role.Value])
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            if (permissions.Length == 0)
            {
                return Task.FromResult(principal);
            }

            var identity = principal.Identities.OfType<ClaimsIdentity>().FirstOrDefault();
            if (identity is null)
            {
                return Task.FromResult(principal);
            }

            foreach (var permission in permissions)
            {
                identity.AddClaim(new Claim(ApiAuthenticationDefaults.PermissionClaimType, permission));
            }

            return Task.FromResult(principal);
        }

        private static void RemoveProviderPermissions(ClaimsPrincipal principal)
        {
            foreach (var identity in principal.Identities.OfType<ClaimsIdentity>())
            {
                foreach (var permission in identity.FindAll(ApiAuthenticationDefaults.PermissionClaimType).ToArray())
                {
                    identity.RemoveClaim(permission);
                }
            }
        }
    }
}
