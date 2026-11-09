using Microsoft.Playwright;

namespace PandaBear.Testing.Tests;

/// <summary>Reports whether the Playwright Chromium browser is installed, for <c>SkipUnless</c> conditions, and starts it for tests that need a real page.</summary>
internal static class ChromiumHost
{
    private static readonly Lazy<bool> Available = new(Probe);

    /// <summary>Gets a value indicating whether Chromium can be launched. Install it with <c>sharedkernel-ci install-playwright</c>. When the <c>SHAREDKERNEL_REQUIRE_BROWSER</c> environment variable is <c>true</c> a failed launch throws instead.</summary>
    public static bool IsInstalled => Available.Value;

    /// <summary>Starts Playwright and a headless Chromium without its own sandbox, which an application started by the test does not need.</summary>
    /// <returns>The Playwright instance and the browser; the caller disposes both.</returns>
    public static async Task<(IPlaywright Playwright, IBrowser Browser)> Launch()
    {
        var playwright = await Microsoft.Playwright.Playwright.CreateAsync().ConfigureAwait(false);
        try
        {
            var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Args = ["--no-sandbox"] }).ConfigureAwait(false);
            return (playwright, browser);
        }
        catch
        {
            playwright.Dispose();
            throw;
        }
    }

    // The browser slice of .config/sharedkernel-ci.json installs Chromium and the CI workflow sets this variable for it, so a missing or broken browser there
    // must fail the tests, not skip them. Other runs, such as the Windows package-consumption job, install no browser and skip.
    private static bool BrowserRequired => string.Equals(Environment.GetEnvironmentVariable("SHAREDKERNEL_REQUIRE_BROWSER"), "true", StringComparison.OrdinalIgnoreCase);

    private static bool Probe()
    {
        try
        {
            // The probe runs once per test run, outside any test's async context, so it blocks on a thread-pool thread.
            return Task.Run(async () =>
            {
                var (playwright, browser) = await Launch().ConfigureAwait(false);
                await browser.DisposeAsync().ConfigureAwait(false);
                playwright.Dispose();
                return true;
            }).GetAwaiter().GetResult();
        }
        catch (PlaywrightException) when (!BrowserRequired)
        {
            return false;
        }
    }
}
