using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace SharedKernel.Testing.Playwright;

/// <summary>Page helpers for Blazor applications.</summary>
public static class PlaywrightPageExtensions
{
    /// <summary>
    /// A CSS selector that matches the hidden interactivity marker (<c>data-interactive='true'</c>) a Blazor layout renders once the page is
    /// interactive. It is a deliberate copy of the marker component's selector so this package does not depend on the Blazor library; a
    /// test in the Blazor test project keeps the two equal.
    /// </summary>
    public const string InteractiveReadySelector = "[data-interactive='true']";

    /// <summary>The element of the ASP.NET Core developer exception page.</summary>
    private const string DeveloperExceptionPageSelector = "#stackpage";

    /// <summary>
    /// Waits until the page shows the interactivity marker (see <see cref="InteractiveReadySelector"/>) in its interactive state, that is, until the Blazor circuit is
    /// connected and event handlers work. Clicking or typing before this point is silently lost on a prerendered page.
    /// </summary>
    /// <param name="page">The page.</param>
    /// <returns>A task that completes when the page is interactive.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="page"/> is <see langword="null"/>.</exception>
    public static Task WaitForBlazorInteractive(this IPage page)
    {
        ArgumentNullException.ThrowIfNull(page);

        return page.Locator(InteractiveReadySelector).WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Attached });
    }

    /// <summary>
    /// Waits until the page is interactive like <see cref="WaitForBlazorInteractive(IPage)"/>, and when it is not, says why: an ASP.NET Core
    /// developer exception page is reported as such, anything else as a timeout with the browser diagnostics (console, page errors, failed
    /// requests, WebSocket lifecycle and the start of the HTML).
    /// </summary>
    /// <param name="page">The page.</param>
    /// <param name="diagnostics">The diagnostics recorded for the page since it was created.</param>
    /// <param name="timeoutMilliseconds">How long to wait, or <see langword="null"/> for Playwright's default.</param>
    /// <returns>A task that completes when the page is interactive.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="page"/> or <paramref name="diagnostics"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">The page is the developer exception page.</exception>
    /// <exception cref="TimeoutException">The page did not become interactive; the message holds the diagnostics.</exception>
    public static async Task WaitForBlazorInteractive(this IPage page, PageDiagnostics diagnostics, float? timeoutMilliseconds = null)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(diagnostics);

        try
        {
            await page.Locator(InteractiveReadySelector)
                .WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Attached, Timeout = timeoutMilliseconds })
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is PlaywrightException or TimeoutException)
        {
            if (await page.Locator(DeveloperExceptionPageSelector).IsVisibleAsync().ConfigureAwait(false))
            {
                throw new InvalidOperationException("The application answered with the ASP.NET Core developer exception page.", exception);
            }

            throw new TimeoutException(
                "The page never became interactive. Browser diagnostics:" + Environment.NewLine + await diagnostics.Describe().ConfigureAwait(false),
                exception);
        }
    }

    /// <summary>
    /// Waits until the page shows an address, for example the list a save returns to. Playwright's assertion polls the page's address in the
    /// driver, so a navigation that completed before the call, or lands while the wait starts, is seen. Use it instead of
    /// <c>IPage.WaitForURLAsync</c>, which reads the address and only then subscribes to navigation events delivered on another thread, and
    /// so times out when the navigation lands in between (microsoft/playwright-dotnet#3364).
    /// </summary>
    /// <param name="page">The page.</param>
    /// <param name="expected">The absolute address the page must show; it is compared with the whole URL, query included.</param>
    /// <returns>A task that completes when the page shows the address.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="page"/> or <paramref name="expected"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="expected"/> is not absolute.</exception>
    /// <exception cref="PlaywrightException">The page did not show the address within the assertion timeout.</exception>
    public static Task ExpectPath(this IPage page, Uri expected)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(expected);
        if (!expected.IsAbsoluteUri)
        {
            throw new ArgumentException("The expected address must be absolute.", nameof(expected));
        }

        return ExpectAbsoluteUri(page, expected.AbsoluteUri);
    }

    /// <summary>
    /// Waits until the page shows a path of an application, resolved against its base address, like <see cref="ExpectPath(IPage, Uri)"/>.
    /// </summary>
    /// <param name="page">The page.</param>
    /// <param name="baseAddress">The absolute base address of the application, such as <c>KestrelPageTest.BaseUri</c>.</param>
    /// <param name="relativePath">The path relative to <paramref name="baseAddress"/>, for example <c>customers</c>.</param>
    /// <returns>A task that completes when the page shows the address.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="page"/>, <paramref name="baseAddress"/> or <paramref name="relativePath"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="baseAddress"/> is not absolute.</exception>
    /// <exception cref="PlaywrightException">The page did not show the address within the assertion timeout.</exception>
    public static Task ExpectPath(this IPage page, Uri baseAddress, string relativePath)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(baseAddress);
        ArgumentNullException.ThrowIfNull(relativePath);
        if (!baseAddress.IsAbsoluteUri)
        {
            throw new ArgumentException("The base address must be absolute.", nameof(baseAddress));
        }

        return page.ExpectPath(new Uri(baseAddress, relativePath));
    }

    /// <summary>Navigates to an address and waits until the page is interactive.</summary>
    /// <param name="page">The page.</param>
    /// <param name="address">The address to open.</param>
    /// <returns>A task that completes when the page is interactive.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="page"/> or <paramref name="address"/> is <see langword="null"/>.</exception>
    public static async Task GotoInteractive(this IPage page, Uri address)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(address);

        await page.GotoAsync(address.ToString(), new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded }).ConfigureAwait(false);
        await page.WaitForBlazorInteractive().ConfigureAwait(false);
    }

    private static async Task ExpectAbsoluteUri(IPage page, string expected)
    {
        try
        {
            await Assertions.Expect(page).ToHaveURLAsync(new Regex(string.Concat("^", Regex.Escape(expected), "$"))).ConfigureAwait(false);
        }
        catch (PlaywrightException exception)
        {
            throw new PlaywrightException(string.Concat("The page did not show ", expected, ". ", exception.Message), exception);
        }
    }
}
