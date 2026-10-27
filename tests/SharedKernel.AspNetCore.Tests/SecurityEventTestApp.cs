using System.Diagnostics.Metrics;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace PandaBear.AspNetCore.Tests;

/// <summary>Starts a web application with real JWT bearer authentication and a rate-limited route.</summary>
internal static class SecurityEventTestApp
{
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
            .AddJwtBearer(options => options.Configuration = new OpenIdConnectConfiguration());
        builder.Services.AddAuthorization();
        builder.Services.AddRateLimiter(options => options.AddRemoteIpFixedWindowPolicy("login", 1, TimeSpan.FromMinutes(1)));
        builder.Services.AddSecurityEventTelemetry();
        var app = builder.Build();
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseRateLimiter();
        app.MapGet("/secure", () => "ok").RequireAuthorization();
        app.MapGet("/login", () => "ok").AllowAnonymous().RequireRateLimiting("login");
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }
}
