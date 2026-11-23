namespace PandaBear.Blazor.Site;

/// <summary>One image of an <see cref="ImageGrid" />.</summary>
/// <param name="Image">The image, with its width, height, accessibility decision and optional responsive sources.</param>
/// <param name="Caption">The optional visible caption.</param>
public sealed record GalleryImage(ResponsiveImage Image, string? Caption = null)
{
    /// <summary>
    /// Gets the optional address the image links to, such as its full-size file or a page about it. With it the grid wraps the image in
    /// a plain <c>a href</c>, named by the image's alternative text, so a described image is required: an <see cref="ImageGrid" /> refuses
    /// a linked decorative image. <see langword="null" />, the default, renders the image without a link.
    /// </summary>
    public LinkTarget? Href { get; init; }

    /// <summary>
    /// Gets the optional short credit of the image, such as its author and licence. The grid renders it as an
    /// <see cref="ImageCreditCaption" /> in the image's <c>figcaption</c>, after the caption and outside the image's link, so the link
    /// keeps the alternative text as its name. <see langword="null" />, the default, renders none.
    /// </summary>
    public ImageCreditLine? Credit { get; init; }
}
