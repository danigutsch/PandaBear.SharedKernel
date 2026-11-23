using Microsoft.Playwright;

namespace PandaBear.Blazor.Tests.Browser;

/// <summary>Opens the picker page in Chromium and finds its controls and the focused one.</summary>
internal static class PickerBrowser
{
    /// <summary>Finds a button of one picker by its exact accessible name.</summary>
    /// <param name="page">The page.</param>
    /// <param name="picker">The picker's id.</param>
    /// <param name="name">The accessible name.</param>
    /// <returns>The locator.</returns>
    public static ILocator PickerButton(this IPage page, string picker, string name) =>
        page.Locator("#" + picker).GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = name, Exact = true });

    /// <summary>Gets the accessible name given by <c>aria-label</c> of the focused element.</summary>
    /// <param name="page">The page.</param>
    /// <returns>The name, or <see langword="null"/> when the focused element has none.</returns>
    public static Task<string?> FocusedName(this IPage page) =>
        page.EvaluateAsync<string?>("() => document.activeElement && document.activeElement.getAttribute('aria-label')");

    /// <summary>Opens the picker page with the first images chosen and waits until it is interactive.</summary>
    /// <param name="browser">The browser.</param>
    /// <param name="host">The host.</param>
    /// <param name="chosen">The number of chosen photos.</param>
    /// <returns>The page; the caller closes it.</returns>
    public static async Task<IPage> OpenPicker(this IBrowser browser, PickerHost host, int chosen)
    {
        var page = await browser.NewPageAsync();
        await page.GotoAsync(host.Page(chosen).ToString());
        await page.WaitForSelectorAsync(InteractiveMarker.ReadySelector, new PageWaitForSelectorOptions { State = WaitForSelectorState.Attached });
        await Assertions.Expect(page.Locator("#photos-find")).ToBeEnabledAsync();
        return page;
    }
}
