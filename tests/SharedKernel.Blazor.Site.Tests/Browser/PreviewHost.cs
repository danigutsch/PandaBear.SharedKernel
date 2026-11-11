using PandaBear.AspNetCore;

namespace PandaBear.Blazor.Site.Tests.Browser;

/// <summary>A loopback Interactive Server host that serves <see cref="PreviewDocument" /> under a policy without <c>'unsafe-inline'</c>.</summary>
internal sealed class PreviewHost : IAsyncDisposable
{
    /// <summary>The policy: styles only from the site or with the response's nonce, scripts only from the site.</summary>
    public const string Policy = "default-src 'self'; script-src 'self'; style-src 'self' 'nonce-{nonce}'; connect-src 'self' ws: wss:; object-src 'none'; base-uri 'self'";

    private readonly WebApplication _app;

    private PreviewHost(WebApplication app) => _app = app;

    /// <summary>Gets the address of the host.</summary>
    public Uri BaseUri => new(_app.Urls.Single() + "/");

    /// <summary>Starts a host.</summary>
    /// <param name="options">How the preview is rendered.</param>
    /// <returns>The started host.</returns>
    public static async Task<PreviewHost> Start(PreviewHostOptions options)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        builder.Services.AddSingleton(options);
        builder.Services.AddCspNonce();
        builder.Services.AddRazorComponents().AddInteractiveServerComponents();
        var app = builder.Build();
        app.UseSecurityHeaders(Policy);
        app.UseAntiforgery();
        app.MapStaticAssets();
        app.MapRazorComponents<PreviewDocument>().AddInteractiveServerRenderMode();
        await app.StartAsync();
        return new PreviewHost(app);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => _app.DisposeAsync();
}
