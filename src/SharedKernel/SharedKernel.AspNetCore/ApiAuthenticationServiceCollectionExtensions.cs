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
using PandaBear.OpenApi;

namespace PandaBear.AspNetCore;

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

    /// <summary>Registers API security with an explicit authentication switch, so tests can force authentication on or off independent of the OpenAPI generation mode.</summary>
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
    /// Adds several named exact-audience bearer schemes for a host that co-hosts modules, or authorization only while
    /// OpenAPI documents are generated. No scheme authenticates by default: every protected route's policy must name
    /// its scheme with <c>AddAuthenticationSchemes</c>, and the fallback policy rejects routes that do not.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration that supplies the authority and issuer.</param>
    /// <param name="environment">The host environment.</param>
    /// <param name="schemes">The schemes; names and audiences must be unique.</param>
    /// <returns>The authorization builder for route policies.</returns>
    public static AuthorizationBuilder AddApiSecurity(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment,
        IReadOnlyList<ApiBearerScheme> schemes)
    {
        return AddApiSecurity(services, configuration, environment, schemes, registerBearerAuthentication: null);
    }

    /// <summary>Registers API security with an explicit authentication switch, so tests can force authentication on or off independent of the OpenAPI generation mode.</summary>
    internal static AuthorizationBuilder AddApiSecurity(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment,
        IReadOnlyList<ApiBearerScheme> schemes,
        bool? registerBearerAuthentication)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);
        ValidateSchemes(schemes);

        var shouldRegisterBearerAuthentication = registerBearerAuthentication
            ?? !OpenApiGenerationMode.IsEnabled(environment);

        return shouldRegisterBearerAuthentication
            ? services.AddApiBearerAuthentication(configuration, environment, schemes)
            : services.AddApiAuthorization(EmptyPermissions);
    }

    /// <summary>
    /// Adds several named exact-audience bearer schemes. Each scheme maps roles with its own permission table when it
    /// validates a token; no host-wide claims transformation is registered.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration that supplies the authority and issuer.</param>
    /// <param name="environment">The host environment.</param>
    /// <param name="schemes">The schemes; names and audiences must be unique.</param>
    /// <returns>The authorization builder for route policies.</returns>
    public static AuthorizationBuilder AddApiBearerAuthentication(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment,
        IReadOnlyList<ApiBearerScheme> schemes)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);
        ValidateSchemes(schemes);

        var authority = configuration[ApiAuthenticationDefaults.AuthorityConfigurationKey];
        var issuer = configuration[ApiAuthenticationDefaults.IssuerConfigurationKey];
        var allowHttpDevelopmentAuthority = environment.IsDevelopment()
            && string.Equals(
                configuration[ApiAuthenticationDefaults.AllowHttpDevelopmentAuthorityConfigurationKey],
                bool.TrueString,
                StringComparison.OrdinalIgnoreCase);

        ValidateConfiguration(authority, issuer, allowHttpDevelopmentAuthority);

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IPostConfigureOptions<JwtBearerOptions>, JwtBearerPostConfigureOptions>());

        // No default authenticate scheme: a route is authenticated only by the scheme its policy names, so a route
        // that forgets a module policy fails closed under the fallback instead of accepting another module's token.
        var authentication = services.AddAuthentication(options =>
        {
            options.DefaultChallengeScheme = schemes[0].Name;
            options.DefaultForbidScheme = schemes[0].Name;
        });
        foreach (var scheme in schemes)
        {
            authentication.AddScheme<JwtBearerOptions, JwtBearerHandler>(scheme.Name, options =>
            {
                ConfigureBearerOptions(options, scheme.Audience, authority, issuer, allowHttpDevelopmentAuthority);
                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = context =>
                    {
                        if (context.Principal is { } principal)
                        {
                            ApplyPermissions(principal, scheme.PermissionsByRole);
                        }

                        return Task.CompletedTask;
                    }
                };
            });
        }

        return services.AddApiAuthorization(EmptyPermissions);
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

    /// <summary>Applies the audience, authority, and token validation settings shared by all JWT bearer registrations.</summary>
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
            AudienceValidator = (audiences, _, _) => HasExactAudience(audiences, audience),
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(2),
            ValidAlgorithms = [SecurityAlgorithms.RsaSha256]
        };
    }

    private static readonly IReadOnlyDictionary<string, IReadOnlyCollection<string>> EmptyPermissions =
        new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.Ordinal);

    private static void ValidateSchemes(IReadOnlyList<ApiBearerScheme> schemes)
    {
        ArgumentNullException.ThrowIfNull(schemes);
        if (schemes.Count == 0)
        {
            throw new ArgumentException("At least one bearer scheme is required.", nameof(schemes));
        }

        foreach (var scheme in schemes)
        {
            ArgumentNullException.ThrowIfNull(scheme);
            ArgumentException.ThrowIfNullOrWhiteSpace(scheme.Name);
            ArgumentException.ThrowIfNullOrWhiteSpace(scheme.Audience);
            ArgumentNullException.ThrowIfNull(scheme.PermissionsByRole);
        }

        if (schemes.Select(static scheme => scheme.Name).Distinct(StringComparer.Ordinal).Count() != schemes.Count)
        {
            throw new ArgumentException("Bearer scheme names must be unique.", nameof(schemes));
        }

        if (schemes.Select(static scheme => scheme.Audience).Distinct(StringComparer.Ordinal).Count() != schemes.Count)
        {
            // One audience per scheme keeps a token for one module from authenticating another module's routes.
            throw new ArgumentException("Bearer scheme audiences must be unique.", nameof(schemes));
        }
    }

    private static void ApplyPermissions(
        ClaimsPrincipal principal,
        IReadOnlyDictionary<string, IReadOnlyCollection<string>> permissionsByRole)
    {
        foreach (var claimsIdentity in principal.Identities.OfType<ClaimsIdentity>())
        {
            foreach (var permission in claimsIdentity.FindAll(ApiAuthenticationDefaults.PermissionClaimType).ToArray())
            {
                claimsIdentity.RemoveClaim(permission);
            }
        }

        var permissions = principal.FindAll(ApiAuthenticationDefaults.RolesClaimType)
            .Where(role => permissionsByRole.ContainsKey(role.Value))
            .SelectMany(role => permissionsByRole[role.Value])
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var identity = principal.Identities.OfType<ClaimsIdentity>().FirstOrDefault();
        if (identity is null)
        {
            return;
        }

        foreach (var permission in permissions)
        {
            identity.AddClaim(new Claim(ApiAuthenticationDefaults.PermissionClaimType, permission));
        }
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

    private static bool HasExactAudience(IEnumerable<string>? audiences, string expectedAudience)
    {
        if (audiences is null)
        {
            return false;
        }

        using var enumerator = audiences.GetEnumerator();
        return enumerator.MoveNext()
               && string.Equals(enumerator.Current, expectedAudience, StringComparison.Ordinal)
               && !enumerator.MoveNext();
    }

    private sealed class PermissionClaimsTransformation(IReadOnlyDictionary<string, IReadOnlyCollection<string>> permissionsByRole)
        : IClaimsTransformation
    {
        public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
        {
            ArgumentNullException.ThrowIfNull(principal);

            ApplyPermissions(principal, permissionsByRole);
            return Task.FromResult(principal);
        }
    }
}
