using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using SharedKernel.AspNetCore.WebAnalytics;

namespace SharedKernel.Aspire.Hosting.Umami.Tests;

/// <summary>A site on a test server that sends its page views to Umami through the Umami page-view target, as a site referencing the website would.</summary>
internal sealed class PageViewTestSite : IAsyncDisposable
{
    /// <summary>The visitor's browser, which Umami does not take for a bot.</summary>
    internal const string VisitorAgent = "Mozilla/5.0 (X11; Linux x86_64; rv:140.0) Gecko/20100101 Firefox/140.0";

    private readonly WebApplication _app;

    private PageViewTestSite(WebApplication app) => _app = app;

    /// <summary>Starts the site with the website's connection string as <c>ConnectionStrings:umami</c>.</summary>
    /// <param name="connectionString">The website's connection string.</param>
    /// <param name="ct">Cancels the start.</param>
    /// <returns>The started site.</returns>
    internal static async Task<PageViewTestSite> Start(string connectionString, CancellationToken ct)
    {
        var builder = WebApplication.CreateBuilder(["--hostBuilder:reloadConfigOnChange=false"]);
        builder.WebHost.UseTestServer();
        builder.Configuration["ConnectionStrings:umami"] = connectionString;
        builder.AddPageViews().AddUmami();

        var app = builder.Build();
        app.Use((context, next) =>
        {
            context.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.7");
            return next(context);
        });
        app.UsePageViews();
        app.MapGet("/{**path}", () => TypedResults.Content("<!doctype html><title>Viagens</title>", "text/html"));
        await app.StartAsync(ct);
        return new PageViewTestSite(app);
    }

    /// <summary>Requests a page as the visitor's browser.</summary>
    /// <param name="pathAndQuery">The path and query.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The answer.</returns>
    internal async Task<HttpResponseMessage> Visit(string pathAndQuery, CancellationToken ct)
    {
        using var client = _app.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(pathAndQuery, UriKind.Relative));
        request.Headers.TryAddWithoutValidation("User-Agent", VisitorAgent);
        return await client.SendAsync(request, ct);
    }

    /// <summary>Stops the site, which first sends what is still queued.</summary>
    /// <param name="ct">Cancels the stop.</param>
    /// <returns>A task that completes when the site has stopped.</returns>
    internal Task Stop(CancellationToken ct) => _app.StopAsync(ct);

    /// <inheritdoc />
    public ValueTask DisposeAsync() => _app.DisposeAsync();
}
