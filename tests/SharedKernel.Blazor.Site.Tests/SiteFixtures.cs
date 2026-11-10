using System.Globalization;
using Microsoft.AspNetCore.Components.Web;
using PandaBear.BuildingBlocks;

namespace PandaBear.Blazor.Site.Tests;

/// <summary>Builds the inputs and renders the hosts that several site component tests share.</summary>
internal static class SiteFixtures
{
    /// <summary>Gets the header navigation used by the header tests: two links and one group.</summary>
    public static SiteNavItem[] NavItems
    {
        get;
    } =
    [
        new("Home", "/"),
        new("Tours", "/tours"),
        new("About", null, [new("Team", "/about/team"), new("Story", "/about/story")]),
    ];

    /// <summary>Creates a validated phone number.</summary>
    /// <param name="value">The number text.</param>
    /// <returns>The number.</returns>
    public static PhoneNumber Phone(string value) => PhoneNumber.Create(value).Value;

    /// <summary>Creates a validated email address.</summary>
    /// <param name="value">The address text.</param>
    /// <returns>The address.</returns>
    public static EmailAddress Email(string value) => EmailAddress.Create(value).Value;

    /// <summary>Creates described gallery images <c>/img1.jpg</c> to <c>/imgN.jpg</c>, 400 by 300, the first one captioned.</summary>
    /// <param name="count">The number of images.</param>
    /// <returns>The images.</returns>
    public static GalleryImage[] Images(int count) => Enumerable.Range(1, count)
        .Select(index => new GalleryImage($"/img{index}.jpg", ImageAlternative.Describe($"Photo {index}"), 400, 300, index == 1 ? "First" : null))
        .ToArray();

    /// <summary>Renders the header with <see cref="NavItems" />, the label "Main" and a brand link.</summary>
    /// <param name="context">The bUnit context.</param>
    /// <param name="currentPath">The current path.</param>
    /// <returns>The rendered header.</returns>
    public static IRenderedComponent<SiteHeader> RenderHeader(this BunitContext context, string? currentPath = null) => context.Render<SiteHeader>(parameters => parameters
        .Add(component => component.Label, "Main")
        .Add(component => component.Brand, "<a href=\"/\">Brand</a>")
        .Add(component => component.Items, NavItems)
        .Add(component => component.CurrentPath, currentPath));

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

    /// <summary>Computes the WCAG contrast ratio of two <c>#rrggbb</c> colours.</summary>
    /// <param name="foreground">The text colour.</param>
    /// <param name="background">The background colour.</param>
    /// <returns>The ratio, from 1 to 21.</returns>
    public static double Contrast(string foreground, string background)
    {
        var first = Luminance(foreground);
        var second = Luminance(background);
        return (Math.Max(first, second) + 0.05) / (Math.Min(first, second) + 0.05);
    }

    private static double Luminance(string hex) =>
        (0.2126 * Channel(hex, 0)) + (0.7152 * Channel(hex, 2)) + (0.0722 * Channel(hex, 4));

    private static double Channel(string hex, int offset)
    {
        var value = int.Parse(hex.AsSpan(1 + offset, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255.0;
        return value <= 0.03928 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
    }
}
