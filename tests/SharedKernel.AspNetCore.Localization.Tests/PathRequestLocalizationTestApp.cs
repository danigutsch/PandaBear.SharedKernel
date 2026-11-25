using System.Globalization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace SharedKernel.AspNetCore.Localization.Tests;

/// <summary>
/// Hosts request localization in front of a terminal endpoint on an in-memory server; the endpoint names the current culture and UI
/// culture in <see cref="CultureHeader" /> as <c>{culture}|{ui culture}</c>.
/// </summary>
internal static class PathRequestLocalizationTestApp
{
    /// <summary>The header in which the endpoint names the request's cultures.</summary>
    public const string CultureHeader = "X-Culture";

    /// <summary>Starts a host.</summary>
    /// <param name="localize">Adds the request localization under test.</param>
    /// <returns>The started host.</returns>
    public static async Task<WebApplication> Start(Action<WebApplication> localize)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        var app = builder.Build();
        localize(app);
        app.Run(static context =>
        {
            context.Response.Headers[CultureHeader] = $"{CultureInfo.CurrentCulture.Name}|{CultureInfo.CurrentUICulture.Name}";
            return context.Response.WriteAsync("ok");
        });
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }

    /// <summary>Sends a GET through the in-memory server.</summary>
    /// <param name="app">The host.</param>
    /// <param name="path">The path.</param>
    /// <param name="configure">Changes the request before it is sent.</param>
    /// <returns>The completed context.</returns>
    public static Task<HttpContext> Send(WebApplication app, string path, Action<HttpRequest>? configure = null)
    {
        var server = (TestServer)app.Services.GetRequiredService<IServer>();
        return server.SendAsync(context =>
        {
            context.Request.Method = HttpMethods.Get;
            context.Request.Path = path;
            configure?.Invoke(context.Request);
        });
    }
}
