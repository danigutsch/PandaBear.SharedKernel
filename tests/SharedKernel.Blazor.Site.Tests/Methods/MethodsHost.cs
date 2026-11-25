using Microsoft.AspNetCore.Components.Endpoints;

namespace SharedKernel.Blazor.Site.Tests.Methods;

/// <summary>
/// A loopback host that maps the routable components of the test assembly, letting them answer GET and HEAD only, except
/// <see cref="MethodsErrorPage" />.
/// </summary>
internal sealed class MethodsHost : IAsyncDisposable
{
    private readonly WebApplication _app;

    private MethodsHost(WebApplication app, HttpClient client)
    {
        _app = app;
        Client = client;
    }

    /// <summary>Gets a client for the host.</summary>
    public HttpClient Client { get; }

    /// <summary>Gets the endpoint of a routed component.</summary>
    /// <typeparam name="TComponent">The component.</typeparam>
    /// <returns>The endpoint.</returns>
    public Endpoint EndpointOf<TComponent>() =>
        _app.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .Single(static endpoint => endpoint.Metadata.OfType<ComponentTypeMetadata>().Any(static page => page.Type == typeof(TComponent)));

    /// <summary>Starts a host.</summary>
    /// <returns>The started host.</returns>
    public static async Task<MethodsHost> Start()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        builder.Services.AddRazorComponents();
        var app = builder.Build();
        app.UseAntiforgery();
        app.MapRazorComponents<MethodsPage>().AllowOnlyGetAndHead(typeof(MethodsErrorPage));
        await app.StartAsync();
#pragma warning disable CA2000 // The handler is disposed with the client.
        var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, CheckCertificateRevocationList = true }) { BaseAddress = new Uri(app.Urls.Single()) };
#pragma warning restore CA2000
        return new MethodsHost(app, client);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await _app.DisposeAsync();
    }
}
