using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright.Xunit.v3;
using PandaBear.Testing.AspNetCore;

namespace PandaBear.Testing.Playwright;

/// <summary>
/// Base class for browser tests that run the application's real entry point on Kestrel and drive it with Playwright's
/// <see cref="PageTest"/> (browser, context and <c>Page</c>). By default each test gets its own application instance, so tests that use
/// their own database stay isolated; override <see cref="EnvironmentName"/>, <see cref="Configuration"/> and
/// <see cref="ConfigureServices"/> to arrange it. A class whose tests can share one application passes a
/// <see cref="KestrelHostFixture{TEntryPoint}"/> to the other constructor instead. Call <see cref="NavigateTo"/> to open a page once it
/// is interactive, and use <see cref="Services"/> to reach the application's services.
/// </summary>
/// <typeparam name="TEntryPoint">A type from the application's entry-point assembly, usually <c>Program</c>.</typeparam>
public abstract class KestrelPageTest<TEntryPoint> : PageTest
    where TEntryPoint : class
{
    private readonly KestrelHostFixture<TEntryPoint>? sharedHost;
    private KestrelTestHost<TEntryPoint>? host;

    /// <summary>Initializes a test that starts an application of its own.</summary>
    protected KestrelPageTest()
    {
    }

    /// <summary>
    /// Initializes a test that uses the application of a class fixture, which the test neither starts nor stops. The test's
    /// <see cref="EnvironmentName"/>, <see cref="Configuration"/> and <see cref="ConfigureServices"/> are then not used; the fixture's are.
    /// </summary>
    /// <param name="sharedHost">The application shared by the tests of the class.</param>
    /// <exception cref="ArgumentNullException"><paramref name="sharedHost"/> is null.</exception>
    protected KestrelPageTest(KestrelHostFixture<TEntryPoint> sharedHost)
    {
        ArgumentNullException.ThrowIfNull(sharedHost);
        this.sharedHost = sharedHost;
    }

    /// <summary>Gets the hosting environment name for the application. Default: <c>Development</c>. Not used with a shared application.</summary>
    protected virtual string EnvironmentName => "Development";

    /// <summary>Gets configuration values added on top of the application's own, or <see langword="null"/> for none. Not used with a shared application.</summary>
    protected virtual IReadOnlyDictionary<string, string?>? Configuration => null;

    /// <summary>Gets the address the application listens on.</summary>
    /// <exception cref="InvalidOperationException">The application has not started yet.</exception>
    protected Uri BaseUri => Host.BaseAddress;

    /// <summary>Gets the application's root service provider, for example to seed data through the application's own services.</summary>
    /// <exception cref="InvalidOperationException">The application has not started yet.</exception>
    protected IServiceProvider Services => Host.Services;

    private KestrelTestHost<TEntryPoint> Host =>
        sharedHost?.Host ?? host ?? throw new InvalidOperationException("The application has not started.");

    /// <inheritdoc />
    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync().ConfigureAwait(false);
        if (sharedHost is null)
        {
            host = KestrelTestHost.Start<TEntryPoint>(EnvironmentName, Configuration, ConfigureServices);
        }
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

    /// <summary>Overrides services after the application's own registrations. Called before the application starts. Not used with a shared application.</summary>
    /// <param name="services">The service collection.</param>
    protected virtual void ConfigureServices(IServiceCollection services)
    {
    }

    /// <summary>Opens a page of the application and waits until it is interactive.</summary>
    /// <param name="relativePath">The path to open, for example <c>customers/new</c>.</param>
    /// <returns>A task that completes when the page is interactive.</returns>
    protected Task NavigateTo(string relativePath) => Page.GotoInteractive(new Uri(BaseUri, relativePath));
}
