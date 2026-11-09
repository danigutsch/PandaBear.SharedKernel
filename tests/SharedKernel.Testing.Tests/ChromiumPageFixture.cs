using Microsoft.Playwright;

namespace PandaBear.Testing.Tests;

/// <summary>Owns one Chromium browser for the tests of a class, when Chromium is installed, and opens pages with given content.</summary>
public sealed class ChromiumPageFixture : IAsyncLifetime
{
    private IPlaywright? playwright;
    private IBrowser? browser;

    /// <inheritdoc />
    public async ValueTask InitializeAsync()
    {
        if (ChromiumHost.IsInstalled)
        {
            (playwright, browser) = await ChromiumHost.Launch();
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (browser is not null)
        {
            await browser.DisposeAsync();
        }

        playwright?.Dispose();
    }

    /// <summary>Opens a page whose document has the given language and body markup.</summary>
    /// <param name="body">The markup inside the body element.</param>
    /// <param name="language">The <c>lang</c> attribute of the root element.</param>
    /// <returns>The page.</returns>
    public async Task<IPage> Show(string body, string language = "en")
    {
        var page = await browser.ShouldNotBeNull().NewPageAsync();
        await page.SetContentAsync($"<!doctype html><html lang=\"{language}\"><head><title>t</title></head><body>{body}</body></html>");
        return page;
    }
}
