namespace PandaBear.Blazor.Site;

/// <summary>How a <see cref="Hero" /> places its text and its image.</summary>
public enum HeroLayout
{
    /// <summary>The text and the image side by side from the breakpoint on, stacked below it, inside the content width.</summary>
    Split = 0,

    /// <summary>
    /// The full width of the page, the text centred over the image, which is darkened by the large-text scrim
    /// (<c>--site-scrim-large</c>); without an image a band of the primary colour. The text is the on-primary colour in the light scheme.
    /// </summary>
    Cover = 1,
}
