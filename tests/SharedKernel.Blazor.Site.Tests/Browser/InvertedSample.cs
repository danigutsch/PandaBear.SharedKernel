using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Playwright;
using PandaBear.Blazor.Site.Theming;

namespace PandaBear.Blazor.Site.Tests.Browser;

/// <summary>
/// A page for the tests of the primary colour's inversion: a primary band with a plain card (outline more look, primary button link) and
/// a boxed card (primary more look, outline button link), then a primary footer with a level 2 heading, a divider and a link.
/// </summary>
public sealed class InvertedSample : ComponentBase
{
    /// <summary>
    /// Opens the sample at 1280px with reduced motion, so a hover shows at once, and a theme stylesheet that colours level 2 headings
    /// and card titles with the primary colour.
    /// </summary>
    /// <param name="browser">The browser.</param>
    /// <returns>The open page.</returns>
    internal static async Task<LayoutPage> Open(IBrowser browser)
    {
        var page = await LayoutPage.Serve<InvertedSample>(browser, 1280);
        await page.Page.EmulateMediaAsync(new PageEmulateMediaOptions { ReducedMotion = ReducedMotion.Reduce });
        var heading = TypographyStyle.Create(color: ThemeColorRole.Primary).ShouldBeSuccess();
        var typography = SiteTheme.Default.Typography.With(TypographyRole.H2, heading).With(TypographyRole.CardTitle, heading);
        var theme = SiteTheme.Create(SiteTheme.Default.Colors, typography).ShouldBeSuccess();
        await page.Page.AddStyleTagAsync(new PageAddStyleTagOptions { Content = SiteThemeStylesheet.Render(theme) });
        return page;
    }

    /// <summary>
    /// Moves the mouse to the centre of an element, as a pointer over it does; unlike a Playwright hover it also reaches an element that
    /// lets the pointer through, such as the more look of a card.
    /// </summary>
    /// <param name="page">The page.</param>
    /// <param name="selector">The CSS selector of the element.</param>
    /// <returns>A task that completes when the mouse has moved and two frames have been drawn, so the hover look has applied.</returns>
    internal static async Task MoveMouseTo(LayoutPage page, string selector)
    {
        ArgumentNullException.ThrowIfNull(page);
        var box = await page.Box(selector);
        await page.Page.Mouse.MoveAsync((float)(box.X + (box.Width / 2)), (float)(box.Y + (box.Height / 2)));
        await page.Page.EvaluateAsync("() => new Promise(done => requestAnimationFrame(() => requestAnimationFrame(done)))");
    }

    /// <summary>Gets a computed colour of the first element that matches a selector.</summary>
    /// <param name="page">The page.</param>
    /// <param name="selector">The CSS selector.</param>
    /// <param name="property">The colour property, such as <c>background-color</c>.</param>
    /// <returns>The colour as lower-case <c>#rrggbb</c>.</returns>
    internal static async Task<string> Color(LayoutPage page, string selector, string property)
    {
        ArgumentNullException.ThrowIfNull(page);
        return ComputedColor.ToHex(await page.Style(selector, property));
    }

    /// <summary>
    /// Gives an element keyboard focus, as the Tab key does, so it matches <c>:focus-visible</c>.
    /// </summary>
    /// <param name="page">The page.</param>
    /// <param name="selector">The CSS selector of the element.</param>
    /// <returns><see langword="true" /> when the element has focus and matches <c>:focus-visible</c>.</returns>
    internal static async Task<bool> FocusWithKeyboard(LayoutPage page, string selector)
    {
        ArgumentNullException.ThrowIfNull(page);
        for (var press = 0; press < 12; press++)
        {
            await page.Page.Keyboard.PressAsync("Tab");
            if (await page.Page.EvaluateAsync<bool>("(selector) => document.activeElement === document.querySelector(selector)", selector))
            {
                return await page.Page.EvaluateAsync<bool>("(selector) => document.querySelector(selector).matches(':focus-visible')", selector);
            }
        }

        return false;
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.OpenElement(0, "main");
        builder.OpenComponent<Band>(1);
        builder.AddComponentParameter(2, nameof(Band.Tone), BandTone.Primary);
        builder.AddComponentParameter(3, nameof(Band.ChildContent), (RenderFragment)(band =>
        {
            band.OpenComponent<CardGrid>(0);
            band.AddComponentParameter(1, nameof(CardGrid.ChildContent), (RenderFragment)(grid =>
            {
                grid.AddContent(0, CardFragment("plain", CardStyle.Plain, ButtonVariant.Outline, ButtonVariant.Primary));
                grid.AddContent(1, CardFragment("boxed", CardStyle.Boxed, ButtonVariant.Primary, ButtonVariant.Outline));
            }));
            band.CloseComponent();
        }));
        builder.CloseComponent();
        builder.CloseElement();
        builder.OpenComponent<SiteFooter>(4);
        builder.AddComponentParameter(5, nameof(SiteFooter.Tone), FooterTone.Primary);
        builder.AddComponentParameter(6, nameof(SiteFooter.ChildContent), (RenderFragment)(footer =>
            footer.AddMarkupContent(0, "<h2 id=\"footer-heading\">Contact</h2><div id=\"footer-divider\" class=\"site-divider\" aria-hidden=\"true\"></div><p><a id=\"footer-link\" href=\"/contact\">Write to us</a></p>")));
        builder.CloseComponent();
    }

    private static RenderFragment CardFragment(string id, CardStyle style, ButtonVariant more, ButtonVariant action) => builder =>
    {
        builder.OpenComponent<Card>(0);
        builder.AddComponentParameter(1, "id", id);
        builder.AddComponentParameter(2, nameof(Card.Title), "Alentejo");
        builder.AddComponentParameter(3, nameof(Card.Href), LinkTarget.FromString("/tours/" + id));
        builder.AddComponentParameter(4, nameof(Card.CardStyle), style);
        builder.AddComponentParameter(5, nameof(Card.MoreText), "More");
        builder.AddComponentParameter(6, nameof(Card.MoreVariant), more);
        builder.AddComponentParameter(7, nameof(Card.Actions), (RenderFragment)(actions =>
        {
            actions.OpenComponent<ButtonLink>(0);
            actions.AddComponentParameter(1, "id", id + "-action");
            actions.AddComponentParameter(2, nameof(ButtonLink.Href), LinkTarget.FromString("/sign-up/" + id));
            actions.AddComponentParameter(3, nameof(ButtonLink.Variant), action);
            actions.AddComponentParameter(4, nameof(ButtonLink.ChildContent), (RenderFragment)(label => label.AddContent(0, "Sign up")));
            actions.CloseComponent();
        }));
        builder.CloseComponent();
    };
}
