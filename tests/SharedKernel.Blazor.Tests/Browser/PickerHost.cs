namespace PandaBear.Blazor.Tests.Browser;

/// <summary>
/// A loopback Interactive Server host of <see cref="PickerDocument"/> over a library of 1000 images, the page of the picker's browser
/// and prerendered-state tests.
/// </summary>
internal sealed class PickerHost : IAsyncDisposable
{
    /// <summary>The number of images in the library.</summary>
    public const int LibrarySize = 1000;

    private readonly WebApplication _app;

    private PickerHost(WebApplication app) => _app = app;

    /// <summary>Gets the address of the host.</summary>
    public Uri BaseUri => new(_app.Urls.Single() + "/");

    /// <summary>Starts a host.</summary>
    /// <returns>The started host.</returns>
    public static async Task<PickerHost> Start()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        builder.Services.AddSingleton(new PickerLibrary(LibrarySize));
        builder.Services.AddRazorComponents().AddInteractiveServerComponents();
        var app = builder.Build();
        app.UseAntiforgery();
        app.MapStaticAssets();
        app.MapRazorComponents<PickerDocument>().AddInteractiveServerRenderMode();
        await app.StartAsync();
        return new PickerHost(app);
    }

    /// <summary>Gets the address of the page with the first images of the library chosen.</summary>
    /// <param name="chosen">The number of chosen images.</param>
    /// <returns>The address.</returns>
    public Uri Page(int chosen) => new(BaseUri, "?chosen=" + chosen.ToString(System.Globalization.CultureInfo.InvariantCulture));

    /// <inheritdoc />
    public ValueTask DisposeAsync() => _app.DisposeAsync();
}
