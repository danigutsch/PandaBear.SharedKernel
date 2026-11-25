using Microsoft.Playwright;

namespace SharedKernel.Testing.Playwright;

/// <summary>
/// Reports whether the Playwright Chromium browser can be launched, for xUnit <c>SkipUnless</c> conditions, and launches it for
/// tests that need a real page:
/// <c>[Fact(SkipType = typeof(Chromium), SkipUnless = nameof(Chromium.IsInstalled), Skip = Chromium.NotInstalledReason)]</c>.
/// </summary>
public static class Chromium
{
    /// <summary>The skip reason of a browser test that cannot run because Chromium is not installed.</summary>
    public const string NotInstalledReason = "The Playwright Chromium browser is not installed; run dotnet ci install-playwright.";

    /// <summary>
    /// The environment variable that, set to <c>true</c>, makes a missing or broken browser fail the tests instead of skipping them,
    /// for a run that installed Chromium on purpose.
    /// </summary>
    public const string RequiredVariable = "REQUIRE_BROWSER_TESTS";

    private static readonly Lazy<bool> Available = new(Probe);

    /// <summary>
    /// Gets a value indicating whether Chromium can be launched. The first read launches it once for the test run. When
    /// <see cref="RequiredVariable"/> is <c>true</c>, a failed launch throws its <see cref="PlaywrightException"/> instead.
    /// </summary>
    /// <exception cref="PlaywrightException">Chromium cannot be launched and <see cref="RequiredVariable"/> is <c>true</c>.</exception>
    public static bool IsInstalled => Available.Value;

    /// <summary>Starts Playwright and a headless Chromium without its own sandbox, which an application started by the test does not need.</summary>
    /// <returns>The Playwright instance and the browser; the caller disposes both.</returns>
    /// <exception cref="PlaywrightException">Chromium cannot be launched.</exception>
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

    private static bool BrowserRequired =>
        string.Equals(Environment.GetEnvironmentVariable(RequiredVariable), "true", StringComparison.OrdinalIgnoreCase);

    private static bool Probe()
    {
        try
        {
            return Task.Run(static async () =>
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
