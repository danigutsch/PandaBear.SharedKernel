using Microsoft.Extensions.DependencyInjection;
using SharedKernel.Testing.AspNetCore;
using Xunit;

namespace SharedKernel.Testing.Playwright;

/// <summary>
/// One run of the application's real entry point on Kestrel, shared by the browser tests of a class. Use it as an xUnit class fixture and
/// pass it to the <see cref="KestrelPageTest{TEntryPoint}(KestrelHostFixture{TEntryPoint})"/> constructor. Override
/// <see cref="EnvironmentName"/>, <see cref="Configuration"/> and <see cref="ConfigureServices"/> in a derived fixture to arrange it, and
/// <see cref="InitializeAsync"/> to prepare what the application needs before it starts.
/// </summary>
/// <remarks>
/// Every test of the class then shares the application's state: its database, caches and singletons. Use it for suites that only read, or
/// whose tests do not depend on what the others wrote. Each test still gets its own browser context and page.
/// </remarks>
/// <typeparam name="TEntryPoint">A type from the application's entry-point assembly, usually <c>Program</c>.</typeparam>
public class KestrelHostFixture<TEntryPoint> : IAsyncLifetime
    where TEntryPoint : class
{
    private KestrelTestHost<TEntryPoint>? host;

    /// <summary>Gets the address the application listens on.</summary>
    /// <exception cref="InvalidOperationException">The application has not started yet.</exception>
    public Uri BaseAddress => Host.BaseAddress;

    /// <summary>Gets the application's root service provider.</summary>
    /// <exception cref="InvalidOperationException">The application has not started yet.</exception>
    public IServiceProvider Services => Host.Services;

    /// <summary>Gets the hosting environment name for the application. Default: <c>Development</c>.</summary>
    protected virtual string EnvironmentName => "Development";

    /// <summary>Gets configuration values added on top of the application's own, or <see langword="null"/> for none.</summary>
    protected virtual IReadOnlyDictionary<string, string?>? Configuration => null;

    /// <summary>Gets the running application.</summary>
    /// <exception cref="InvalidOperationException">The application has not started yet.</exception>
    internal KestrelTestHost<TEntryPoint> Host => host ?? throw new InvalidOperationException("The application has not started.");

    /// <summary>Starts the application.</summary>
    /// <returns>A task that completes when the application listens.</returns>
    public virtual ValueTask InitializeAsync()
    {
        host = KestrelTestHost.Start<TEntryPoint>(EnvironmentName, Configuration, ConfigureServices);
        return ValueTask.CompletedTask;
    }

    /// <summary>Stops the application.</summary>
    /// <returns>A task that completes when the application has stopped.</returns>
    public virtual async ValueTask DisposeAsync()
    {
        if (host is not null)
        {
            await host.DisposeAsync().ConfigureAwait(false);
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>Overrides services after the application's own registrations. Called before the application starts.</summary>
    /// <param name="services">The service collection.</param>
    protected virtual void ConfigureServices(IServiceCollection services)
    {
    }
}
