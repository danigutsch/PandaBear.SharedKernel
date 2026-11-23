using Microsoft.Playwright;

namespace PandaBear.Blazor.Tests.Browser;

/// <summary>Owns one Chromium browser for the tests of a class, when Chromium is installed.</summary>
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
        if (ChromiumHost.IsInstalled)
        {
            (_playwright, _browser) = await ChromiumHost.Launch();
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_browser is not null)
        {
            await _browser.DisposeAsync();
        }

        _playwright?.Dispose();
    }
}
