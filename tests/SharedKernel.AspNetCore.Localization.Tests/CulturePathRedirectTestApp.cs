using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel.BuildingBlocks;

namespace SharedKernel.AspNetCore.Localization.Tests;

/// <summary>Hosts the culture path redirect middleware in front of a terminal endpoint on an in-memory server.</summary>
internal static class CulturePathRedirectTestApp
{
    /// <summary>Starts a host whose terminal endpoint responds <c>ok</c> after the redirect middleware.</summary>
    public static async Task<WebApplication> Start(
        SupportedCultures supported,
        string cookieName = CulturePathRedirectExtensions.DefaultCookieName,
        IReadOnlyCollection<PathString>? excludedPrefixes = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        var app = builder.Build();
        app.UseCulturePathRedirect(supported, cookieName, excludedPrefixes);
        app.Run(static context => context.Response.WriteAsync("ok"));
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }

    /// <summary>Sends a request through the in-memory server without following redirects.</summary>
    public static Task<HttpContext> Send(WebApplication app, string method, string path, Action<HttpRequest>? configure = null)
    {
        var server = (TestServer)app.Services.GetRequiredService<IServer>();
        return server.SendAsync(context =>
        {
            context.Request.Method = method;
            context.Request.Path = path;
            configure?.Invoke(context.Request);
        });
    }
}
