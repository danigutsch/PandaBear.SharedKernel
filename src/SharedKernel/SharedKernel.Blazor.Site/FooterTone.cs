namespace SharedKernel.Blazor.Site;

/// <summary>The background of a <see cref="SiteFooter" />.</summary>
public enum FooterTone
{
    /// <summary>The page background in the content width, under a border, with muted text.</summary>
    Default = 0,

    /// <summary>
    /// A full-width band of the primary colour in the light scheme (<c>site-footer--primary</c>): on-primary text and links, an
    /// on-primary focus ring, and the band's own padding with the content in the content width.
    /// </summary>
    Primary = 1,
}
