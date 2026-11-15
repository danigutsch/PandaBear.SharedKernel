using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Playwright;

namespace PandaBear.Blazor.Site.Tests.Browser;

/// <summary>
/// The statically rendered <see cref="LayoutSample" /> styled by the package's <c>site.css</c>, open in Chromium at a viewport width,
/// with the measurements the layout tests compare.
/// </summary>
internal sealed class LayoutPage : IAsyncDisposable
{
    private LayoutPage(IPage page) => Page = page;

    /// <summary>Gets the page.</summary>
    public IPage Page { get; }

    /// <summary>Renders the sample as static server-side rendering does and opens it with the stylesheet inlined.</summary>
    /// <param name="browser">The browser.</param>
    /// <param name="width">The viewport width in CSS pixels.</param>
    /// <param name="cards">The number of cards in the grid.</param>
    /// <returns>The open page.</returns>
    public static async Task<LayoutPage> Open(IBrowser browser, int width, int cards = 1)
    {
        var css = await File.ReadAllTextAsync(SiteFiles.StylesheetPath());
        var body = await Render(cards);
        var page = await browser.NewPageAsync(new BrowserNewPageOptions { ViewportSize = new ViewportSize { Width = width, Height = 900 }, ColorScheme = ColorScheme.Light });
        await page.SetContentAsync($"<!DOCTYPE html><html lang=\"en\"><head><meta charset=\"utf-8\"><title>Layout</title><style>{css}</style></head><body>{body}</body></html>");
        return new LayoutPage(page);
    }

    /// <summary>Gets the box of the first element that matches a selector.</summary>
    /// <param name="selector">The CSS selector.</param>
    /// <returns>The box.</returns>
    /// <exception cref="InvalidOperationException">The element is not rendered.</exception>
    public async Task<ElementBox> Box(string selector) => ToBox(await Page.Locator(selector).First.BoundingBoxAsync(), selector);

    /// <summary>Gets the boxes of every element that matches a selector, in document order.</summary>
    /// <param name="selector">The CSS selector.</param>
    /// <returns>The boxes.</returns>
    /// <exception cref="InvalidOperationException">An element is not rendered.</exception>
    public async Task<IReadOnlyList<ElementBox>> Boxes(string selector)
    {
        var boxes = new List<ElementBox>();
        foreach (var element in await Page.Locator(selector).AllAsync())
        {
            boxes.Add(ToBox(await element.BoundingBoxAsync(), selector));
        }

        return boxes;
    }

    /// <summary>Gets whether the first element that matches a selector is visible.</summary>
    /// <param name="selector">The CSS selector.</param>
    /// <returns><see langword="true" /> when it is rendered with a non-empty box.</returns>
    public Task<bool> IsVisible(string selector) => Page.Locator(selector).First.IsVisibleAsync();

    /// <summary>Gets an attribute of the first element that matches a selector.</summary>
    /// <param name="selector">The CSS selector.</param>
    /// <param name="name">The attribute name.</param>
    /// <returns>The value, or <see langword="null" /> when the attribute is missing.</returns>
    public Task<string?> Attribute(string selector, string name) => Page.Locator(selector).First.GetAttributeAsync(name);

    /// <summary>Gets a computed style property of the first element that matches a selector, or of one of its pseudo-elements.</summary>
    /// <param name="selector">The CSS selector.</param>
    /// <param name="property">The CSS property.</param>
    /// <param name="pseudo">The pseudo-element, such as <c>::after</c>, or <see langword="null" />.</param>
    /// <returns>The computed value.</returns>
    public Task<string> Style(string selector, string property, string? pseudo = null) => Page.EvaluateAsync<string>(
        "([selector, property, pseudo]) => getComputedStyle(document.querySelector(selector), pseudo).getPropertyValue(property)",
        new[] { selector, property, pseudo });

    /// <summary>Gets whether the navigation group is open.</summary>
    /// <returns><see langword="true" /> when the <c>details</c> element is open.</returns>
    public Task<bool> GroupIsOpen() => Page.EvaluateAsync<bool>("() => document.querySelector('.site-nav__group').open");

    /// <summary>Opens the navigation group with a click on its summary, as a user does.</summary>
    /// <returns>A task that completes when the group is open.</returns>
    public async Task OpenGroup()
    {
        await Page.ClickAsync(".site-nav__group > summary");
        await Assertions.Expect(Page.Locator(".site-nav__group[open]")).ToHaveCountAsync(1);
    }

    /// <summary>Gets the width of the document's content, which exceeds the viewport when the page scrolls sideways.</summary>
    /// <returns>The scroll width in CSS pixels.</returns>
    public Task<int> ScrollWidth() => Page.EvaluateAsync<int>("() => document.documentElement.scrollWidth");

    /// <inheritdoc />
    public ValueTask DisposeAsync() => new(Page.CloseAsync());

    private static ElementBox ToBox(LocatorBoundingBoxResult? box, string selector) => box is null
        ? throw new InvalidOperationException($"An element of {selector} is not rendered.")
        : new ElementBox(Math.Round(box.X, 1), Math.Round(box.Y, 1), Math.Round(box.Width, 1), Math.Round(box.Height, 1));

    private static async Task<string> Render(int cards)
    {
        await using var services = new ServiceCollection().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, NullLoggerFactory.Instance);
        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var parameters = ParameterView.FromDictionary(new Dictionary<string, object?> { [nameof(LayoutSample.Cards)] = cards });
            var output = await renderer.RenderComponentAsync<LayoutSample>(parameters);
            return output.ToHtmlString();
        });
    }
}
