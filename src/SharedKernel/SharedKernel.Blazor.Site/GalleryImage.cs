namespace SharedKernel.Blazor.Site;

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

    /// <summary>
    /// Gets the optional reviewed credit. The grid uses it to show attribution and access to the full details next to the photo.
    /// Use this property when the UI and metadata share a reviewed rights record. Set only one of this property and <see cref="Credit" />.
    /// </summary>
    public PhotoCreditItem? ReviewedCredit { get; init; }

    /// <summary>
    /// Gets the optional BCP 47 tag of the alternative text and caption. The image and its link, if present, carry the tag so
    /// assistive technology can determine the language of their accessible names. Credit text keeps its surrounding language.
    /// <see langword="null" /> lets the text inherit the surrounding language.
    /// </summary>
    public string? TextLanguage { get; init; }
}
