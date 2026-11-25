using Microsoft.Playwright;
using Xunit;

namespace SharedKernel.Testing.Playwright;

/// <summary>
/// Owns one Chromium browser for the tests of a class, when <see cref="Chromium.IsInstalled"/> is <see langword="true"/>. Use it as
/// an xUnit class fixture and skip the tests with <see cref="Chromium.NotInstalledReason"/> when Chromium is not installed.
/// </summary>
public sealed class ChromiumFixture : IAsyncLifetime
{
    private IPlaywright? _playwright;
    private IBrowser? _browser;

    /// <summary>Gets the browser.</summary>
    /// <exception cref="InvalidOperationException">Chromium is not installed.</exception>
    public IBrowser Browser => _browser ?? throw new InvalidOperationException("Chromium is not installed.");

    /// <inheritdoc />
    public async ValueTask InitializeAsync()
    {
        if (Chromium.IsInstalled)
        {
            (_playwright, _browser) = await Chromium.Launch().ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_browser is not null)
        {
            await _browser.DisposeAsync().ConfigureAwait(false);
        }

        _playwright?.Dispose();
    }
}
