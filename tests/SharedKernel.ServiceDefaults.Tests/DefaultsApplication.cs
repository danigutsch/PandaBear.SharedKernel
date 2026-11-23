using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;

namespace PandaBear.ServiceDefaults.Tests;

/// <summary>Builds an in-memory web application that uses the service defaults and maps <c>/ping</c>.</summary>
internal static class DefaultsApplication
{
    /// <summary>The configuration key of the OTLP endpoint.</summary>
    internal const string OtlpEndpointKey = "OTEL_EXPORTER_OTLP_ENDPOINT";

    /// <summary>Builds and starts the application.</summary>
    /// <param name="environment">The host environment name.</param>
    /// <param name="configure">Changes the service defaults settings.</param>
    /// <param name="arrange">Changes the builder after the service defaults are added.</param>
    /// <returns>The started application.</returns>
    internal static async Task<WebApplication> Start(
        string environment,
        Action<ServiceDefaultsOptions>? configure = null,
        Action<WebApplicationBuilder>? arrange = null)
    {
        var app = Build(environment, configure, arrange);
        app.MapHealthEndpoints();
        app.MapGet("/ping", () =>
        {
            var activity = Activity.Current;
            activity?.SetTag("email", "private@example.test");
            activity?.SetTag("app.secret", "private");
            activity?.SetTag("app.kept", "public");
            return "pong";
        });
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }

    /// <summary>Builds the application without mapping endpoints.</summary>
    /// <param name="environment">The host environment name.</param>
    /// <param name="configure">Changes the service defaults settings.</param>
    /// <param name="arrange">Changes the builder after the service defaults are added.</param>
    /// <returns>The application.</returns>
    internal static WebApplication Build(
        string environment,
        Action<ServiceDefaultsOptions>? configure = null,
        Action<WebApplicationBuilder>? arrange = null)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = environment });
        builder.WebHost.UseTestServer();

        // An OTLP endpoint in the test process's environment must not decide what the tests see.
        builder.Configuration[OtlpEndpointKey] = string.Empty;
        builder.AddApplicationDefaults(configure);

        // After the defaults, as a host adds its own exporters, so the privacy processors run first.
        arrange?.Invoke(builder);
        return builder.Build();
    }
}
