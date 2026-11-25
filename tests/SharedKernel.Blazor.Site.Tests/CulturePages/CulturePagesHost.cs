using System.Net;

namespace SharedKernel.Blazor.Site.Tests.CulturePages;

/// <summary>A loopback host that renders the culture page tests' pages statically through <see cref="CulturePagesRoot" />.</summary>
internal sealed class CulturePagesHost : IAsyncDisposable
{
    private readonly WebApplication _app;

    private CulturePagesHost(WebApplication app, HttpClient client)
    {
        _app = app;
        Client = client;
    }

    /// <summary>Gets a client for the host.</summary>
    public HttpClient Client { get; }

    /// <summary>Gets the reader the pages read their content from.</summary>
    public CulturePageReader Reader => _app.Services.GetRequiredService<CulturePageReader>();

    /// <summary>Starts a host.</summary>
    /// <returns>The started host.</returns>
    public static async Task<CulturePagesHost> Start()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        builder.Services.AddRazorComponents();
        builder.Services.AddSingleton<CulturePageReader>();
        var app = builder.Build();
        app.UseAntiforgery();
        app.MapRazorComponents<CulturePagesRoot>();
        await app.StartAsync();
#pragma warning disable CA2000 // The handler is disposed with the client.
        var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, CheckCertificateRevocationList = true }) { BaseAddress = new Uri(app.Urls.Single()) };
#pragma warning restore CA2000
        return new CulturePagesHost(app, client);
    }

    /// <summary>Requests a path.</summary>
    /// <param name="path">The root-relative path.</param>
    /// <returns>The status code and body.</returns>
    public async Task<(HttpStatusCode Status, string Body)> Get(string path)
    {
        using var response = await Client.GetAsync(new Uri(path, UriKind.Relative), Xunit.TestContext.Current.CancellationToken);
        return (response.StatusCode, await response.Content.ReadAsStringAsync(Xunit.TestContext.Current.CancellationToken));
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await _app.DisposeAsync();
    }
}
