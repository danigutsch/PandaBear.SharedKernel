using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace SharedKernel.Testing.AspNetCore;

/// <summary>
/// Configures real JWT bearer validation and authenticated test clients for API host tests.
/// </summary>
public static class ApiTestAuthentication
{
    /// <summary>
    /// The HTTPS authority and issuer used only by in-process API test hosts.
    /// </summary>
    public const string Authority = "https://identity.test";

    private const string RoleClaimType = "roles";
    private static readonly SymmetricSecurityKey SigningKey = new(RandomNumberGenerator.GetBytes(32));

    /// <summary>
    /// Replaces discovery-backed JWT validation with a test-only signed-token validator.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="audience">The audience accepted by the test host.</param>
    public static void ConfigureJwtBearer(IServiceCollection services, string audience)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(audience);

        services.PostConfigureAll<JwtBearerOptions>(options =>
        {
            options.Authority = null;
            options.ConfigurationManager = null;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = Authority,
                ValidateAudience = true,
                ValidAudience = audience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = SigningKey,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero,
                ValidAlgorithms = [SecurityAlgorithms.HmacSha256]
            };
        });
    }

    /// <summary>
    /// Adds a valid bearer token with the supplied provider role to a test client.
    /// </summary>
    /// <param name="client">The HTTP client to configure.</param>
    /// <param name="audience">The intended API audience.</param>
    /// <param name="role">The provider role to emit.</param>
    public static void ConfigureAuthenticatedClient(HttpClient client, string audience, string role)
    {
        ConfigureClient(client, audience, Authority, role);
    }

    /// <summary>
    /// Adds a signed bearer token with caller-specified issuer, audience, and role claims to a test client.
    /// </summary>
    /// <param name="client">The HTTP client to configure.</param>
    /// <param name="audience">The token audience.</param>
    /// <param name="issuer">The token issuer.</param>
    /// <param name="role">The provider role to emit.</param>
    public static void ConfigureClient(HttpClient client, string audience, string issuer, string role)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentException.ThrowIfNullOrWhiteSpace(audience);
        ArgumentException.ThrowIfNullOrWhiteSpace(issuer);
        ArgumentException.ThrowIfNullOrWhiteSpace(role);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken(audience, issuer, role));
    }

    private static string CreateToken(string audience, string issuer, string role)
    {
        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, "test-user"),
                new Claim(RoleClaimType, role)
            ],
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: new SigningCredentials(SigningKey, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
