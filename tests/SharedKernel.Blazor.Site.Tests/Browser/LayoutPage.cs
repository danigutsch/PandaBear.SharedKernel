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
    /// <param name="languages">Whether the header holds a language switcher.</param>
    /// <param name="theme">Rules written after the stylesheet, as a theme stylesheet is.</param>
    /// <returns>The open page.</returns>
    public static async Task<LayoutPage> Open(IBrowser browser, int width, int cards = 1, bool languages = false, string theme = "")
    {
        var css = await File.ReadAllTextAsync(SiteFiles.StylesheetPath()) + theme;
        var body = await Render(cards, languages);
        var page = await browser.NewPageAsync(new BrowserNewPageOptions { ViewportSize = new ViewportSize { Width = width, Height = 900 }, ColorScheme = ColorScheme.Light });
        await page.SetContentAsync($"<!DOCTYPE html><html lang=\"en\"><head><meta charset=\"utf-8\"><title>Layout</title><style>{css}</style></head><body>{body}</body></html>");
        return new LayoutPage(page);
    }

    /// <summary>
    /// Renders a component as static server-side rendering does and serves it from <c>http://site.test/</c> with the stylesheet
    /// inlined, and every other address of that site as an all-white photo, 9 by 16 for <see cref="GallerySample.TallPhoto" /> and 16 by
    /// 9 otherwise, so images load
    /// from root-relative addresses and nothing leaves the browser.
    /// </summary>
    /// <typeparam name="TComponent">The component.</typeparam>
    /// <param name="browser">The browser.</param>
    /// <param name="width">The viewport width in CSS pixels.</param>
    /// <param name="parameters">The component parameters.</param>
    /// <returns>The open page, with every image of the first viewport loaded.</returns>
    public static async Task<LayoutPage> Serve<TComponent>(IBrowser browser, int width, IDictionary<string, object?>? parameters = null)
        where TComponent : IComponent
    {
        var css = await File.ReadAllTextAsync(SiteFiles.StylesheetPath());
        var body = await Render<TComponent>(parameters ?? new Dictionary<string, object?>());
        var html = $"<!DOCTYPE html><html lang=\"en\"><head><meta charset=\"utf-8\"><title>Bands</title><style>{css}</style></head><body>{body}</body></html>";
        var page = await browser.NewPageAsync(new BrowserNewPageOptions { ViewportSize = new ViewportSize { Width = width, Height = 900 }, ColorScheme = ColorScheme.Light });
        await page.RouteAsync("**/*", route => route.Request.Url == "http://site.test/"
            ? route.FulfillAsync(new RouteFulfillOptions { ContentType = "text/html; charset=utf-8", Body = html })
            : route.Request.Url.StartsWith("http://site.test/", StringComparison.Ordinal)
                ? route.FulfillAsync(new RouteFulfillOptions { ContentType = "image/svg+xml", Body = route.Request.Url.EndsWith(GallerySample.TallPhoto, StringComparison.Ordinal) ? WhiteSvg(9, 16) : WhiteSvg(16, 9) })
                : route.AbortAsync());
        await page.GotoAsync("http://site.test/");
        return new LayoutPage(page);
    }

    /// <summary>Gets the colour of one pixel of the rendered page in viewport coordinates, from a screenshot.</summary>
    /// <param name="x">The left offset in CSS pixels.</param>
    /// <param name="y">The top offset in CSS pixels.</param>
    /// <returns>The colour as lower-case <c>#rrggbb</c>.</returns>
    public async Task<string> Pixel(double x, double y)
    {
        var png = await Page.ScreenshotAsync(new PageScreenshotOptions { Clip = new Clip { X = (float)x, Y = (float)y, Width = 1, Height = 1 }, Animations = ScreenshotAnimations.Disabled });
        return await Page.EvaluateAsync<string>(
            """
            async (data) => {
                const image = new Image();
                image.src = 'data:image/png;base64,' + data;
                await image.decode();
                const canvas = document.createElement('canvas');
                canvas.width = 1;
                canvas.height = 1;
                const context = canvas.getContext('2d');
                context.drawImage(image, 0, 0);
                const [red, green, blue] = context.getImageData(0, 0, 1, 1).data;
                return '#' + [red, green, blue].map(value => value.toString(16).padStart(2, '0')).join('');
            }
            """,
            Convert.ToBase64String(png));
    }

    /// <summary>Scrolls an element into view and waits until every image in it has loaded.</summary>
    /// <param name="selector">The CSS selector.</param>
    /// <returns>A task that completes when the images are decoded.</returns>
    public async Task ShowLoaded(string selector)
    {
        await Page.Locator(selector).First.ScrollIntoViewIfNeededAsync();
        await Page.WaitForFunctionAsync(
            "(selector) => [...document.querySelector(selector).querySelectorAll('img')].every(image => image.complete && image.naturalWidth > 0)",
            selector);
    }

    /// <summary>Gets the number of navigation landmarks that assistive technology can reach.</summary>
    /// <returns>The count of visible elements with the navigation role.</returns>
    public Task<int> NavigationLandmarks() => Page.GetByRole(AriaRole.Navigation).CountAsync();

    /// <summary>Gets the nodes that Chromium exposes to assistive technology, from its full accessibility tree without ignored nodes.</summary>
    /// <returns>The nodes in tree order.</returns>
    public async Task<IReadOnlyList<AccessibleNode>> AccessibilityTree()
    {
        var session = await Page.Context.NewCDPSessionAsync(Page);
        var tree = await session.SendAsync("Accessibility.getFullAXTree");
        var nodes = new List<AccessibleNode>();
        foreach (var node in tree!.Value.GetProperty("nodes").EnumerateArray())
        {
            if (node.GetProperty("ignored").GetBoolean() || !node.TryGetProperty("role", out var role))
            {
                continue;
            }

            var name = node.TryGetProperty("name", out var nameValue) && nameValue.TryGetProperty("value", out var text) ? text.GetString() ?? string.Empty : string.Empty;
            bool? expanded = null;
            if (node.TryGetProperty("properties", out var properties))
            {
                foreach (var property in properties.EnumerateArray().Where(property => property.GetProperty("name").GetString() == "expanded"))
                {
                    expanded = property.GetProperty("value").GetProperty("value").GetBoolean();
                }
            }

            nodes.Add(new AccessibleNode(role.GetProperty("value").GetString() ?? string.Empty, name, expanded));
        }

        await session.DetachAsync();
        return nodes;
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

    private static string WhiteSvg(int width, int height) =>
        $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{width}\" height=\"{height}\"><rect width=\"{width}\" height=\"{height}\" fill=\"#ffffff\"/></svg>";

    private static async Task<string> Render<TComponent>(IDictionary<string, object?> parameters)
        where TComponent : IComponent
    {
        await using var services = new ServiceCollection().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, NullLoggerFactory.Instance);
        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var output = await renderer.RenderComponentAsync<TComponent>(ParameterView.FromDictionary(parameters));
            return output.ToHtmlString();
        });
    }

    private static async Task<string> Render(int cards, bool languages)
    {
        await using var services = new ServiceCollection().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, NullLoggerFactory.Instance);
        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var parameters = ParameterView.FromDictionary(new Dictionary<string, object?> { [nameof(LayoutSample.Cards)] = cards, [nameof(LayoutSample.Languages)] = languages });
            var output = await renderer.RenderComponentAsync<LayoutSample>(parameters);
            return output.ToHtmlString();
        });
    }
}
