using System.Diagnostics.Metrics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PandaBear.Testing.Logging;
using PandaBear.Testing.Metrics;

namespace PandaBear.AspNetCore.WebAnalytics.Tests;

/// <summary>
/// A site on a test server that records page views into a fake Umami: every path answers an HTML page, except <c>/site.css</c>
/// (a stylesheet), <c>/missing</c> (404) and <c>/moved</c> (302), and <c>POST /form</c> answers an HTML page too.
/// </summary>
internal sealed class PageViewTestApp : IAsyncDisposable
{
    /// <summary>The configuration key of the Umami endpoint.</summary>
    internal const string EndpointKey = "Umami:Endpoint";

    /// <summary>The page every HTML route answers.</summary>
    internal const string PageHtml = "<!doctype html><html lang=\"pt\"><title>Viagens</title><h1>Viagens</h1></html>";

    private readonly WebApplication _app;

    private PageViewTestApp(WebApplication app, RecordingLoggerProvider logs, MeterCollector meters)
    {
        _app = app;
        Logs = logs;
        Meters = meters;
    }

    /// <summary>Gets the log entries of the forwarder and of its HTTP client, at every level.</summary>
    internal RecordingLoggerProvider Logs { get; }

    /// <summary>Gets the measurements of the site.</summary>
    internal MeterCollector Meters { get; }

    /// <summary>Starts the site.</summary>
    /// <param name="umami">The fake Umami that receives the page views.</param>
    /// <param name="endpoint">The Umami endpoint.</param>
    /// <param name="configure">Further forwarder settings.</param>
    /// <returns>The started site.</returns>
    internal static async Task<PageViewTestApp> Start(HttpMessageHandler umami, string endpoint = "http://umami.test/", Action<UmamiPageViewOptions>? configure = null)
    {
        var logs = new RecordingLoggerProvider();
        var meters = new MeterCollector(WebAnalyticsTelemetry.MeterName);
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Logging.AddProvider(logs);
        builder.Logging.SetMinimumLevel(LogLevel.None);
        builder.Logging.AddFilter("PandaBear", LogLevel.Trace);
        builder.Logging.AddFilter("System.Net.Http.HttpClient", LogLevel.Trace);
        builder.Configuration[EndpointKey] = endpoint;
        builder.Services.AddSingleton<IMeterFactory>(meters);
        builder.Services.AddUmamiPageViews<FixedWebsiteIdProvider>(EndpointKey, configure);
        builder.Services.AddHttpClient(UmamiPageViewServiceCollectionExtensions.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => umami);

        var app = builder.Build();
        app.Use((context, next) =>
        {
            context.Connection.RemoteIpAddress = PageContexts.VisitorAddress;
            return next(context);
        });
        app.UseUmamiPageViews();
        app.MapGet("/site.css", () => TypedResults.Text("body { color: black; }", "text/css"));
        app.MapGet("/missing", () => TypedResults.NotFound());
        app.MapGet("/moved", () => TypedResults.Redirect("/pt"));
        app.MapPost("/form", () => TypedResults.Content(PageHtml, "text/html"));
        app.MapGet("/{**path}", () => TypedResults.Content(PageHtml, "text/html"));
        await app.StartAsync(TestContext.Current.CancellationToken);
        return new PageViewTestApp(app, logs, meters);
    }

    /// <summary>Requests a path as the visitor's browser.</summary>
    /// <param name="method">The method.</param>
    /// <param name="path">The path and query.</param>
    /// <param name="headers">Further request headers.</param>
    /// <returns>The answer.</returns>
    internal async Task<HttpResponseMessage> Visit(HttpMethod method, string path, params (string Name, string Value)[] headers)
    {
        using var client = _app.GetTestClient();
        using var request = new HttpRequestMessage(method, new Uri(path, UriKind.Relative));
        request.Headers.TryAddWithoutValidation("User-Agent", PageContexts.VisitorAgent);
        foreach (var (name, value) in headers)
        {
            request.Headers.TryAddWithoutValidation(name, value);
        }

        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _app.DisposeAsync();
        Logs.Dispose();
        Meters.Dispose();
    }
}
