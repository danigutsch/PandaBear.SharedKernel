using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright.Xunit.v3;
using PandaBear.Testing.AspNetCore;

namespace PandaBear.Testing.Playwright;

/// <summary>
/// Base class for browser tests that run the application's real entry point on Kestrel and drive it with Playwright's
/// <see cref="PageTest"/> (browser, context and <c>Page</c>). Each test gets its own application instance, so tests that use
/// their own database stay isolated. Override <see cref="EnvironmentName"/>, <see cref="Configuration"/> and
/// <see cref="ConfigureServices"/> to arrange it; call <see cref="NavigateTo"/> to open a page once it is interactive.
/// </summary>
/// <typeparam name="TEntryPoint">A type from the application's entry-point assembly, usually <c>Program</c>.</typeparam>
public abstract class KestrelPageTest<TEntryPoint> : PageTest
    where TEntryPoint : class
{
    private KestrelTestHost<TEntryPoint>? host;

    /// <summary>Gets the hosting environment name for the application. Default: <c>Development</c>.</summary>
    protected virtual string EnvironmentName => "Development";

    /// <summary>Gets configuration values added on top of the application's own, or <see langword="null"/> for none.</summary>
    protected virtual IReadOnlyDictionary<string, string?>? Configuration => null;

    /// <summary>Gets the address the application listens on.</summary>
    /// <exception cref="InvalidOperationException">The test has not started yet.</exception>
    protected Uri BaseUri => (host ?? throw new InvalidOperationException("The application has not started.")).BaseAddress;

    /// <inheritdoc />
    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync().ConfigureAwait(false);
        host = KestrelTestHost.Start<TEntryPoint>(EnvironmentName, Configuration, ConfigureServices);
    }

    /// <inheritdoc />
    public override async ValueTask DisposeAsync()
    {
        try
        {
            if (host is not null)
            {
                await host.DisposeAsync().ConfigureAwait(false);
            }
        }
        finally
        {
            await base.DisposeAsync().ConfigureAwait(false);
            GC.SuppressFinalize(this);
        }
    }

    /// <summary>Overrides services after the application's own registrations. Called before the application starts.</summary>
    /// <param name="services">The service collection.</param>
    protected virtual void ConfigureServices(IServiceCollection services)
    {
    }

    /// <summary>Opens a page of the application and waits until it is interactive.</summary>
    /// <param name="relativePath">The path to open, for example <c>customers/new</c>.</param>
    /// <returns>A task that completes when the page is interactive.</returns>
    protected Task NavigateTo(string relativePath) => Page.GotoInteractive(new Uri(BaseUri, relativePath));
}
