using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace PandaBear.Testing.Playwright;

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
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="page"/> or <paramref name="expectedTitle"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="expectedTitle"/> is empty or whitespace.</exception>
    public static Task ShouldHaveTitle(this IPage page, string expectedTitle)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedTitle);

        return Expect(page).ToHaveTitleAsync(expectedTitle);
    }

    /// <summary>
    /// Verifies that the page title eventually matches the expected value.
    /// </summary>
    /// <param name="page">The page to inspect.</param>
    /// <param name="expectedTitle">The expected document title.</param>
    /// <param name="userMessage">Why the test expects the title; Playwright puts it before the failure message.</param>
    /// <returns>A task that completes when the title matches.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="page"/> or <paramref name="expectedTitle"/> is null. Also thrown when <paramref name="userMessage"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="expectedTitle"/> is empty or whitespace.</exception>
    public static Task ShouldHaveTitle(this IPage page, string expectedTitle, string userMessage)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedTitle);
        ArgumentNullException.ThrowIfNull(userMessage);

        return Expect(page, userMessage).ToHaveTitleAsync(expectedTitle);
    }
}
