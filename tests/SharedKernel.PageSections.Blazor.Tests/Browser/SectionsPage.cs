using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Playwright;

namespace PandaBear.PageSections.Blazor.Tests.Browser;

/// <summary>
/// Sections rendered as static server-side rendering does, styled by the site package's <c>site.css</c> and open in Chromium at a
/// viewport width, with the measurements the browser tests compare.
/// </summary>
internal sealed class SectionsPage : IAsyncDisposable
{
    private SectionsPage(IPage page) => Page = page;

    /// <summary>Gets the page.</summary>
    public IPage Page { get; }

    /// <summary>
    /// Renders the sections and serves them from <c>http://site.test/</c> with the stylesheet inlined, and every other address of that
    /// site as an all-white 16 by 9 picture, so images load from root-relative addresses and nothing leaves the browser.
    /// </summary>
    /// <param name="browser">The browser.</param>
    /// <param name="width">The viewport width in CSS pixels.</param>
    /// <param name="sections">The sections.</param>
    /// <param name="textAlignment">The alignment of the bands' text, or <see langword="null" /> to inherit.</param>
    /// <returns>The open page, with every image of the first viewport loaded.</returns>
    public static async Task<SectionsPage> Open(IBrowser browser, int width, IReadOnlyList<PageSection<int, string, NoExtra>> sections, SiteAlignment? textAlignment = null)
    {
        var css = await File.ReadAllTextAsync(StylesheetPath());
        var body = await Render(new Dictionary<string, object?>
        {
            [nameof(PageSectionsView<int, string, NoExtra>.Sections)] = sections,
            [nameof(PageSectionsView<int, string, NoExtra>.Image)] = (Func<int, ResponsiveImage?>)SectionFixtures.Image,
            [nameof(PageSectionsView<int, string, NoExtra>.Link)] = (Func<string, LinkTarget?>)SectionFixtures.Link,
            [nameof(PageSectionsView<int, string, NoExtra>.VideoSiteHintFormat)] = SectionFixtures.HintFormat,
            [nameof(PageSectionsView<int, string, NoExtra>.TextAlignment)] = textAlignment,
        });
        var html = $"<!DOCTYPE html><html lang=\"pt-BR\"><head><meta charset=\"utf-8\"><title>Sections</title><style>{css}</style></head><body><main>{body}</main></body></html>";
        var page = await browser.NewPageAsync(new BrowserNewPageOptions { ViewportSize = new ViewportSize { Width = width, Height = 900 }, ColorScheme = ColorScheme.Light });
        await page.RouteAsync("**/*", route => route.Request.Url == "http://site.test/"
            ? route.FulfillAsync(new RouteFulfillOptions { ContentType = "text/html; charset=utf-8", Body = html })
            : route.Request.Url.StartsWith("http://site.test/", StringComparison.Ordinal)
                ? route.FulfillAsync(new RouteFulfillOptions { ContentType = "image/svg+xml", Body = WhiteSvg })
                : route.AbortAsync());
        await page.GotoAsync("http://site.test/");
        return new SectionsPage(page);
    }

    /// <summary>Gets the box of the first element that matches a selector.</summary>
    /// <param name="selector">The CSS selector.</param>
    /// <returns>The box.</returns>
    /// <exception cref="InvalidOperationException">The element is not rendered.</exception>
    public async Task<LocatorBoundingBoxResult> Box(string selector) =>
        await Page.Locator(selector).First.BoundingBoxAsync() ?? throw new InvalidOperationException($"An element of {selector} is not rendered.");

    /// <summary>Gets a computed style property of the first element that matches a selector.</summary>
    /// <param name="selector">The CSS selector.</param>
    /// <param name="property">The CSS property.</param>
    /// <returns>The computed value.</returns>
    public Task<string> Style(string selector, string property) => Page.EvaluateAsync<string>(
        "([selector, property]) => getComputedStyle(document.querySelector(selector)).getPropertyValue(property)",
        new[] { selector, property });

    /// <inheritdoc />
    public ValueTask DisposeAsync() => new(Page.CloseAsync());

    private const string WhiteSvg = "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"16\" height=\"9\"><rect width=\"16\" height=\"9\" fill=\"#ffffff\"/></svg>";

    private static string StylesheetPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "SharedKernel.slnx")))
        {
            directory = directory.Parent;
        }

        return directory is null
            ? throw new InvalidOperationException("The repository root was not found.")
            : Path.Combine(directory.FullName, "src", "SharedKernel", "SharedKernel.Blazor.Site", "wwwroot", "site.css");
    }

    private static async Task<string> Render(IDictionary<string, object?> parameters)
    {
        await using var services = new ServiceCollection().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, NullLoggerFactory.Instance);
        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var output = await renderer.RenderComponentAsync<PageSectionsView<int, string, NoExtra>>(ParameterView.FromDictionary(parameters));
            return output.ToHtmlString();
        });
    }
}
