using System.Diagnostics.Metrics;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace PandaBear.AspNetCore.Tests;

/// <summary>Starts a web application with real JWT bearer authentication and a rate-limited route.</summary>
internal static class SecurityEventTestApp
{
    // Generated per process so no key material is stored in source.
    private static readonly SymmetricSecurityKey SigningKey = new(RandomNumberGenerator.GetBytes(32));

    /// <summary>Creates a validly signed token that carries no claim, so it authenticates but fails the <c>admin</c> policy.</summary>
    /// <returns>The compact token.</returns>
    public static string CreateValidToken() => new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
    {
        Issuer = "test-issuer",
        Audience = "test-audience",
        Subject = new System.Security.Claims.ClaimsIdentity([new System.Security.Claims.Claim("sub", "user-1")]),
        SigningCredentials = new SigningCredentials(SigningKey, SecurityAlgorithms.HmacSha256)
    });

    /// <summary>Starts the application with security-event telemetry registered.</summary>
    /// <param name="meters">The meter factory and collector.</param>
    /// <param name="logger">The logger that records reporter output.</param>
    /// <returns>The started application.</returns>
    public static async Task<WebApplication> Start(MeterCollector meters, RecordingLogger<SecurityEventReporter> logger)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton<IMeterFactory>(meters);
        builder.Services.AddSingleton<ILogger<SecurityEventReporter>>(logger);
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Configuration = new OpenIdConnectConfiguration();
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    IssuerSigningKey = SigningKey,
                    ValidIssuer = "test-issuer",
                    ValidAudience = "test-audience"
                };
            });
        builder.Services.AddAuthorization(options => options.AddPolicy("admin", policy => policy.RequireClaim("role", "admin")));
        builder.Services.AddRateLimiter(options => options.AddRemoteIpFixedWindowPolicy("login", 1, TimeSpan.FromMinutes(1)));
        builder.Services.AddSecurityEventTelemetry();
        var app = builder.Build();
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseRateLimiter();
        app.MapGet("/secure", () => "ok").RequireAuthorization();
        app.MapGet("/admin", () => "ok").RequireAuthorization("admin");
        app.MapGet("/login", () => "ok").AllowAnonymous().RequireRateLimiting("login");
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }
}
