using Microsoft.AspNetCore.Components;
using PandaBear.BuildingBlocks;

namespace PandaBear.Blazor.Site;

/// <summary>
/// One page of a photo album: a <see cref="Section" /> named by the album's heading, an <see cref="ImageGrid" /> of the page's photos,
/// optional content such as a link to the related tour, and the <see cref="Pager" />.
/// </summary>
/// <remarks>
/// The heading's id is <c>album-</c> and the slug, so it stays the same on every page and in every render and can be linked to. The first
/// <see cref="EagerCount" /> photos in document order load eagerly and the rest lazily, each with its width and height so the layout does
/// not shift. The
/// pager renders plain links and nothing when there is one page. All text is caller-supplied; when the album's title fell back to another
/// language, give its tag in <see cref="TitleLanguage" />, which marks the heading alone. Extra attributes and <c>Class</c> go to the section.
/// </remarks>
public sealed partial class AlbumSection
{
    /// <summary>
    /// The default <see cref="EagerCount" /> of the <see cref="ImageGridLayout.Grid" /> layout: its first row at the default content width
    /// (72rem holds four 14rem columns with their gaps).
    /// </summary>
    public const int DefaultEagerCount = 4;

    /// <summary>
    /// The default <see cref="EagerCount" /> of the <see cref="ImageGridLayout.Mosaic" /> layout. A mosaic's columns run down the page in
    /// document order, so the next photos are below the first one in its first column, and which photos open the other columns depends on
    /// the photos' heights and the viewport; only the first photo is surely in the first row.
    /// </summary>
    public const int DefaultMosaicEagerCount = 1;

    /// <summary>Gets or sets the album title, the section's heading.</summary>
    [Parameter]
    [EditorRequired]
    public string Title { get; set; } = string.Empty;

    /// <summary>Gets or sets the album's slug, which gives the heading its id <c>album-{slug}</c>.</summary>
    [Parameter]
    [EditorRequired]
    public Slug? Slug { get; set; }

    /// <summary>
    /// Gets or sets the BCP 47 language tag of the title when it is in another language than the page; written as <c>lang</c> on the
    /// heading alone, so the photos' text and the pager keep the page's language. <see langword="null"/> writes no <c>lang</c>.
    /// </summary>
    [Parameter]
    public string? TitleLanguage { get; set; }

    /// <summary>Gets or sets the heading level, from 1 to 6. The default is 2; use 1 when the album is the page.</summary>
    [Parameter]
    public int Level { get; set; } = 2;

    /// <summary>Gets or sets an optional introduction under the heading.</summary>
    [Parameter]
    public string? Intro { get; set; }

    /// <summary>Gets or sets the photos of the current page, in display order.</summary>
    [Parameter]
    [EditorRequired]
    public IReadOnlyList<GalleryImage> Photos { get; set; } = [];

    /// <summary>Gets or sets the page window the photos belong to.</summary>
    [Parameter]
    [EditorRequired]
    public PageWindow Window { get; set; }

    /// <summary>Gets or sets the address of each page from its number; see <see cref="Pager.PageHref" />.</summary>
    [Parameter]
    [EditorRequired]
    public Func<int, LinkTarget>? PageHref { get; set; }

    /// <summary>Gets or sets the accessible name of the pager, for example "Album pages".</summary>
    [Parameter]
    [EditorRequired]
    public string PagerLabel { get; set; } = string.Empty;

    /// <summary>Gets or sets the text of the link to the previous page.</summary>
    [Parameter]
    [EditorRequired]
    public string PreviousText { get; set; } = string.Empty;

    /// <summary>Gets or sets the text of the link to the next page.</summary>
    [Parameter]
    [EditorRequired]
    public string NextText { get; set; } = string.Empty;

    /// <summary>Gets or sets the optional accessible name format of each numbered page link; see <see cref="Pager.PageLabelFormat" />.</summary>
    [Parameter]
    public string? PageLabelFormat { get; set; }

    /// <summary>
    /// Gets or sets how many leading photos, in document order, load eagerly; in a mosaic that counts down the first column.
    /// <see langword="null" />, the default, picks <see cref="DefaultEagerCount" /> for a grid and <see cref="DefaultMosaicEagerCount" />
    /// for a mosaic.
    /// </summary>
    [Parameter]
    public int? EagerCount { get; set; }

    /// <summary>
    /// Gets or sets whether the first photo is the page's main image, which then loads eagerly with <c>fetchpriority="high"</c>
    /// whatever <see cref="EagerCount" /> says. The default is <see langword="false" />.
    /// </summary>
    [Parameter]
    public bool PrioritizeFirstImage { get; set; }

    /// <summary>Gets or sets how many pages the pager links on each side of the current page; see <see cref="Pager.SurroundingPages" />. The default is 1.</summary>
    [Parameter]
    public int SurroundingPages { get; set; } = 1;

    /// <summary>Gets or sets how the photos are arranged. The default is <see cref="ImageGridLayout.Grid" />.</summary>
    [Parameter]
    public ImageGridLayout Layout { get; set; }

    /// <summary>Gets or sets optional content after the photos and before the pager, such as a link to the related tour.</summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    private string HeadingId => "album-" + Slug!.Value;

    private int EffectiveEagerCount => EagerCount ?? (Layout == ImageGridLayout.Mosaic ? DefaultMosaicEagerCount : DefaultEagerCount);

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Title);
        ArgumentNullException.ThrowIfNull(Slug);
        ArgumentNullException.ThrowIfNull(Photos);
        if (EagerCount is { } eagerCount)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(eagerCount, nameof(EagerCount));
        }
    }
}
