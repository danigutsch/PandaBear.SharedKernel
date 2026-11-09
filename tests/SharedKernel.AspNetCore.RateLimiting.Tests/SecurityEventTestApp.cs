using System.Diagnostics.Metrics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace PandaBear.AspNetCore.RateLimiting.Tests;

/// <summary>Starts a web application with a rate-limited route and rate-limit event telemetry.</summary>
internal static class SecurityEventTestApp
{
    /// <summary>Starts the application with rate-limit event telemetry registered.</summary>
    /// <param name="meters">The meter factory and collector.</param>
    /// <param name="logger">The logger that records reporter output.</param>
    /// <returns>The started application.</returns>
    public static async Task<WebApplication> Start(MeterCollector meters, RecordingLogger<SecurityEventReporter> logger)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton<IMeterFactory>(meters);
        builder.Services.AddSingleton<ILogger<SecurityEventReporter>>(logger);
        builder.Services.AddRateLimiter(options => options.AddRemoteIpFixedWindowPolicy("login", 1, TimeSpan.FromMinutes(1)));
        builder.Services.AddRateLimitEventTelemetry();
        var app = builder.Build();
        app.UseRouting();
        app.UseRateLimiter();
        app.MapGet("/login", () => "ok").AllowAnonymous().RequireRateLimiting("login");
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }
}
