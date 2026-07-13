using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace SharedKernel.Testing.Playwright;

/// <summary>
/// Provides Playwright assertions for browser test suites.
/// </summary>
public static class PlaywrightShouldAssertionExtensions
{
    /// <summary>
    /// Verifies that the page title eventually matches the expected value.
    /// </summary>
    /// <param name="page">The page to inspect.</param>
    /// <param name="expectedTitle">The expected document title.</param>
    /// <returns>A task that completes when the title matches.</returns>
    public static Task ShouldHaveTitle(this IPage page, string expectedTitle)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedTitle);

        return Expect(page).ToHaveTitleAsync(expectedTitle);
    }
}
