using Microsoft.AspNetCore.Mvc.Testing;

namespace PandaBear.Testing.AspNetCore;

/// <summary>
/// An application's real entry point running on Kestrel at a loopback address with an ephemeral port, so a real browser (or any
/// out-of-process client) can reach it. Start one with <see cref="KestrelTestHost.Start{TEntryPoint}"/>.
/// </summary>
/// <typeparam name="TEntryPoint">A type from the application's entry-point assembly, usually <c>Program</c>.</typeparam>
public sealed class KestrelTestHost<TEntryPoint> : IAsyncDisposable
    where TEntryPoint : class
{
    private readonly WebApplicationFactory<TEntryPoint> factory;

    /// <summary>Initializes the host around an already started factory.</summary>
    /// <param name="factory">The started factory.</param>
    /// <param name="baseAddress">The address it listens on.</param>
    internal KestrelTestHost(WebApplicationFactory<TEntryPoint> factory, Uri baseAddress)
    {
        this.factory = factory;
        BaseAddress = baseAddress;
    }

    /// <summary>Gets the loopback address the server listens on, for example <c>http://127.0.0.1:41873/</c>.</summary>
    public Uri BaseAddress
    {
        get;
    }

    /// <summary>Gets the application's root service provider.</summary>
    public IServiceProvider Services => factory.Services;

    /// <inheritdoc />
    public async ValueTask DisposeAsync() => await factory.DisposeAsync().ConfigureAwait(false);
}
