namespace SharedKernel.Blazor.Site;

/// <summary>Where a <see cref="SiteHeader" /> lies.</summary>
public enum HeaderPlacement
{
    /// <summary>Above the page content, on the surface colour.</summary>
    Above = 0,

    /// <summary>
    /// From the <c>60rem</c> header breakpoint on, transparent over the top of a <see cref="HeroLayout.Cover" /> hero that opens the page: the
    /// first child of the element right after the header, usually <c>main</c>. The hero makes room for it, and over a photo the
    /// header adds the scrim boost (<c>--site-scrim-boost</c>), so its text keeps 4.5:1. On a page that does not open with a cover
    /// hero, and below the breakpoint, the header stays above the content as a band of the primary colour.
    /// </summary>
    OverHero = 1,
}
