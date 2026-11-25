using Microsoft.AspNetCore.Components.Web;
using SharedKernel.Blazor.Site.Theming;
using SharedKernel.BuildingBlocks;

namespace SharedKernel.Blazor.Site.Tests;

/// <summary>Builds the inputs and renders the hosts that several site component tests share.</summary>
internal static class SiteFixtures
{
    /// <summary>Gets the header navigation used by the header tests: two links and one group.</summary>
    public static SiteNavItem[] NavItems { get; } =
    [
        new("Home", Link("/")),
        new("Tours", Link("/tours")),
        new("About", null, [new("Team", Link("/about/team")), new("Story", Link("/about/story"))]),
    ];

    /// <summary>Creates a link target from an author-controlled literal.</summary>
    /// <param name="value">The address.</param>
    /// <returns>The target.</returns>
    public static LinkTarget Link(string value) => LinkTarget.FromString(value);

    /// <summary>Creates a validated phone number.</summary>
    /// <param name="value">The number text.</param>
    /// <returns>The number.</returns>
    public static PhoneNumber Phone(string value) => PhoneNumber.Create(value).Value;

    /// <summary>Creates a validated content culture.</summary>
    /// <param name="value">The culture name.</param>
    /// <returns>The culture, in canonical casing.</returns>
    public static ContentCulture Culture(string value) => ContentCulture.Create(value).Value;

    /// <summary>Creates a validated email address.</summary>
    /// <param name="value">The address text.</param>
    /// <returns>The address.</returns>
    public static EmailAddress Email(string value) => EmailAddress.Create(value).Value;

    /// <summary>Creates described gallery images <c>/img1.jpg</c> to <c>/imgN.jpg</c>, 400 by 300, the first one captioned.</summary>
    /// <param name="count">The number of images.</param>
    /// <returns>The images.</returns>
    public static GalleryImage[] Images(int count) => Enumerable.Range(1, count)
        .Select(index => new GalleryImage(Image($"/img{index}.jpg", ImageAlternative.Describe($"Photo {index}")), index == 1 ? "First" : null))
        .ToArray();

    /// <summary>Creates a 400 by 300 image with one source.</summary>
    /// <param name="src">The address.</param>
    /// <param name="alt">The accessibility decision; described as "Photo" when omitted.</param>
    /// <returns>The image.</returns>
    public static ResponsiveImage Image(string src, ImageAlternative? alt = null) =>
        ResponsiveImage.Create(Link(src), 400, 300, alt ?? ImageAlternative.Describe("Photo"));

    /// <summary>
    /// Creates a 1600 by 900 photo at <c>/p.jpg</c> with AVIF and WebP sources at 800 and 1600 pixels, a JPEG <c>srcset</c> and
    /// <c>sizes</c>.
    /// </summary>
    /// <returns>The image.</returns>
    public static ResponsiveImage PictureImage() => ResponsiveImage.Create(
        Link("/p.jpg"),
        1600,
        900,
        ImageAlternative.Describe("A lake"),
        sizes: "(min-width: 48rem) 50vw, 100vw",
        srcSet: [ImageCandidate.AtWidth(Link("/p-800.jpg"), 800), ImageCandidate.AtWidth(Link("/p.jpg"), 1600)],
        sources:
        [
            ImageSource.Create("image/avif", [ImageCandidate.AtWidth(Link("/p-800.avif"), 800), ImageCandidate.AtWidth(Link("/p-1600.avif"), 1600)]),
            ImageSource.Create("image/webp", [ImageCandidate.AtWidth(Link("/p-800.webp"), 800), ImageCandidate.AtWidth(Link("/p-1600.webp"), 1600)])
        ]);

    /// <summary>Renders the header with <see cref="NavItems" />, the label "Main" and a brand link.</summary>
    /// <param name="context">The bUnit context.</param>
    /// <param name="currentPath">The current path.</param>
    /// <returns>The rendered header.</returns>
    public static IRenderedComponent<SiteHeader> RenderHeader(this BunitContext context, string? currentPath = null) => context.Render<SiteHeader>(parameters => parameters
        .Add(component => component.Label, "Main")
        .Add(component => component.Brand, "<a href=\"/\">Brand</a>")
        .Add(component => component.Items, NavItems)
        .Add(component => component.CurrentPath, currentPath));

    /// <summary>Renders the header with the label "Main" and the given items.</summary>
    /// <param name="context">The bUnit context.</param>
    /// <param name="items">The navigation items.</param>
    /// <returns>The rendered header.</returns>
    public static IRenderedComponent<SiteHeader> RenderHeaderItems(this BunitContext context, params SiteNavItem[] items) => context.Render<SiteHeader>(parameters => parameters
        .Add(component => component.Label, "Main")
        .Add(component => component.Items, items));

    /// <summary>Renders a video link to a YouTube address with a decorative self-hosted poster unless another value is given.</summary>
    /// <param name="context">The bUnit context.</param>
    /// <param name="href">The video address.</param>
    /// <param name="title">The title.</param>
    /// <param name="hint">The site hint.</param>
    /// <param name="poster">The poster; a decorative <c>/poster.jpg</c> when omitted.</param>
    /// <returns>The rendered link.</returns>
    public static IRenderedComponent<VideoLink> RenderVideoLink(
        this BunitContext context,
        string href = "https://www.youtube.com/watch?v=abc123",
        string title = "Patagonia in winter",
        string hint = "(opens YouTube)",
        ResponsiveImage? poster = null) => context.Render<VideoLink>(parameters => parameters
        .Add(component => component.Href, Link(href))
        .Add(component => component.Title, title)
        .Add(component => component.SiteHint, hint)
        .Add(component => component.Poster, poster ?? Image("/poster.jpg", ImageAlternative.Decorative())));

    /// <summary>Renders a head outlet, then a <see cref="PageHead" /> that writes into it.</summary>
    /// <param name="context">The bUnit context.</param>
    /// <param name="configure">The page head parameters.</param>
    /// <returns>The rendered outlet.</returns>
    public static IRenderedComponent<HeadOutlet> RenderHead(this BunitContext context, Action<ComponentParameterCollectionBuilder<PageHead>> configure)
    {
        // HeadOutlet asks the browser for the existing title once; bUnit has no browser, so that call returns a default.
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        var outlet = context.Render<HeadOutlet>();
        context.Render(configure);
        return outlet;
    }

    /// <summary>Computes the WCAG contrast ratio of two <c>#rrggbb</c> colours with the production math of <see cref="CssColor" />.</summary>
    /// <param name="foreground">The text colour.</param>
    /// <param name="background">The background colour.</param>
    /// <returns>The ratio, from 1 to 21.</returns>
    public static double Contrast(string foreground, string background) =>
        CssColor.FromString(foreground).ContrastRatio(CssColor.FromString(background));

    /// <summary>Computes the contrast of the default theme's light on-primary colour, white, on a background.</summary>
    /// <param name="background">The background as <c>#rrggbb</c>.</param>
    /// <returns>The ratio, from 1 to 21.</returns>
    public static double OnPrimaryContrast(string background) => Contrast("#ffffff", background);
}
