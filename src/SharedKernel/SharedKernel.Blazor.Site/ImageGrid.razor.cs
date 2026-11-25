using Microsoft.AspNetCore.Components;

namespace SharedKernel.Blazor.Site;

/// <summary>
/// A grid, a mosaic or thumbnails of images, each a <c>figure</c> with an optional caption. Images load lazily except the first <see cref="EagerCount" />,
/// and every image carries its width and height so the layout does not shift while it loads. An image with a
/// <see cref="GalleryImage.Href" /> is wrapped in a plain link named by its alternative text, which works without script.
/// </summary>
public sealed partial class ImageGrid
{
    /// <summary>
    /// The <c>sizes</c> of the <see cref="ImageGridLayout.Thumbnails" /> layout at the default tokens, so the browser fetches a rendition
    /// of the tile's size. It follows the column count of a grid of the content width with a 1rem gutter on each side: from a 74rem
    /// viewport on six columns of the 72rem content width (a tile under 12rem); from the <c>48rem</c> breakpoint six columns (under 16vw);
    /// from 30rem four to six (under 23vw); from 22.5rem three or four (under 31vw); and below that two (under 45vw). Every value is at
    /// least the tile's width and at most about one and a half times it.
    /// </summary>
    public const string ThumbnailSizes = "(min-width: 74rem) 12rem, (min-width: 48rem) 16vw, (min-width: 30rem) 23vw, (min-width: 22.5rem) 31vw, 45vw";

    private const int MaxSizesLength = 1024;

    /// <summary>Gets or sets the images.</summary>
    [Parameter]
    [EditorRequired]
    public IReadOnlyList<GalleryImage> Images { get; set; } = [];

    /// <summary>Gets or sets how the images are arranged. The default is <see cref="ImageGridLayout.Grid" />.</summary>
    [Parameter]
    public ImageGridLayout Layout { get; set; }

    /// <summary>Gets or sets how many leading images load eagerly, for example those visible without scrolling. The default is 0.</summary>
    [Parameter]
    public int EagerCount { get; set; }

    /// <summary>
    /// Gets or sets whether the first image is the page's main image: it then loads eagerly with <c>fetchpriority="high"</c>,
    /// whatever <see cref="EagerCount" /> says. The default is <see langword="false" />.
    /// </summary>
    [Parameter]
    public bool PrioritizeFirstImage { get; set; }

    private string RootClass => Layout switch
    {
        ImageGridLayout.Mosaic => "site-image-grid site-image-grid--mosaic",
        ImageGridLayout.Thumbnails => "site-image-grid site-image-grid--thumbnails",
        _ => "site-image-grid",
    };

    /// <summary>
    /// Gets or sets the <c>sizes</c> written for every image that has width descriptors, instead of the image's own. <see langword="null" />,
    /// the default, keeps each image's own sizes, except in the <see cref="ImageGridLayout.Thumbnails" /> layout, which writes
    /// <see cref="ThumbnailSizes" /> because the tile, not the image, sets the rendered width. Set it when the grid is narrower than the
    /// content width or the thumbnail tokens are changed.
    /// </summary>
    [Parameter]
    public string? Sizes { get; set; }

    /// <summary>Gets or sets the localized labels required when rendering a reviewed photo credit.</summary>
    [Parameter]
    public PhotoCreditLabels? CreditLabels { get; set; }

    private string? EffectiveSizes => Sizes ?? (Layout == ImageGridLayout.Thumbnails ? ThumbnailSizes : null);

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException">
    /// <see cref="Images" /> or an image is null, or a reviewed credit needs <see cref="CreditLabels" />.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException"><see cref="EagerCount" /> is negative or <see cref="Layout" /> is not defined.</exception>
    /// <exception cref="ArgumentException">
    /// <see cref="Sizes" /> is blank, too long or not one line, a linked image is decorative, an image supplies both credit shapes,
    /// or an image language tag, credit or label is invalid.
    /// </exception>
    protected override void OnParametersSet()
    {
        ArgumentNullException.ThrowIfNull(Images);
        ArgumentOutOfRangeException.ThrowIfNegative(EagerCount);
        EnumParameter.RequireDefined(Layout, nameof(Layout), "The image grid layout is not defined.");

        var usesReviewedCredit = false;
        foreach (var image in Images)
        {
            ArgumentNullException.ThrowIfNull(image, nameof(Images));
            ArgumentNullException.ThrowIfNull(image.Image, nameof(Images));
            LanguageTag.Require(image.TextLanguage, nameof(Images));
            if (image.Href is not null && image.Image.Alt.IsDecorative)
            {
                throw new ArgumentException("A linked image needs alternative text, which names its link; describe the image instead of marking it decorative.", nameof(Images));
            }

            if (image.Credit is not null)
            {
                if (image.ReviewedCredit is not null)
                {
                    throw new ArgumentException("An image can supply only one credit.", nameof(Images));
                }

                ImageCreditLine.Require(image.Credit, nameof(Images));
            }

            if (image.ReviewedCredit is { } reviewed)
            {
                reviewed.Require(nameof(Images));
                usesReviewedCredit |= PhotoCredit.HasCompactCredit(reviewed);
            }
        }

        if (usesReviewedCredit)
        {
            ArgumentNullException.ThrowIfNull(CreditLabels, nameof(CreditLabels));
            CreditLabels.Require(nameof(CreditLabels));
        }

        if (Sizes is not null && (string.IsNullOrWhiteSpace(Sizes) || Sizes.Length > MaxSizesLength || Sizes.Any(char.IsControl)))
        {
            throw new ArgumentException($"Sizes must be one line of at most {MaxSizesLength} characters.", nameof(Sizes));
        }
    }
}
