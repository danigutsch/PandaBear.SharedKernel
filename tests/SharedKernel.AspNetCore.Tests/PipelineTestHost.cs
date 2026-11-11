using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace PandaBear.AspNetCore.Tests;

/// <summary>A loopback host whose pipeline a test builds, with a client that never follows redirects.</summary>
internal sealed class PipelineTestHost : IAsyncDisposable
{
    private readonly WebApplication _app;

    private PipelineTestHost(WebApplication app, HttpClient client)
    {
        _app = app;
        Client = client;
    }

    /// <summary>Gets a client for the host; it does not follow redirects.</summary>
    internal HttpClient Client
    {
        get;
    }

    /// <summary>Builds and starts a host.</summary>
    /// <param name="configure">Adds the middleware and endpoints under test.</param>
    /// <returns>The started host.</returns>
    internal static Task<PipelineTestHost> Start(Action<WebApplication> configure) => Start(static _ => { }, configure);

    /// <summary>Builds and starts a host with services.</summary>
    /// <param name="services">Adds the services under test.</param>
    /// <param name="configure">Adds the middleware and endpoints under test.</param>
    /// <returns>The started host.</returns>
    internal static async Task<PipelineTestHost> Start(Action<IServiceCollection> services, Action<WebApplication> configure)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        services(builder.Services);
        var app = builder.Build();
        configure(app);
        await app.StartAsync();
#pragma warning disable CA2000 // The handler is disposed with the client.
        var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, CheckCertificateRevocationList = true }) { BaseAddress = new Uri(app.Urls.Single()) };
#pragma warning restore CA2000
        return new PipelineTestHost(app, client);
    }

    /// <summary>Sends a request.</summary>
    /// <param name="method">The method.</param>
    /// <param name="path">The path and query.</param>
    /// <param name="configure">Changes the request before it is sent.</param>
    /// <returns>The response.</returns>
    internal async Task<HttpResponseMessage> Send(HttpMethod method, string path, Action<HttpRequestMessage>? configure = null)
    {
        using var request = new HttpRequestMessage(method, path);
        configure?.Invoke(request);
        return await Client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await _app.DisposeAsync();
    }
}
