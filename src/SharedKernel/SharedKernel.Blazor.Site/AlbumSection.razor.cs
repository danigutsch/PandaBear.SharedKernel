using Microsoft.AspNetCore.Components;
using PandaBear.BuildingBlocks;

namespace PandaBear.Blazor.Site;

/// <summary>
/// One page of a photo album: a <see cref="Section" /> named by the album's heading, an <see cref="ImageGrid" /> of the page's photos,
/// optional content such as a link to the related tour, and the <see cref="Pager" />.
/// </summary>
/// <remarks>
/// The heading's id is <c>album-</c> and the slug, so it stays the same on every page and in every render and can be linked to. The first
/// <see cref="EagerCount" /> photos load eagerly and the rest lazily, each with its width and height so the layout does not shift. The
/// pager renders plain links and nothing when there is one page. All text is caller-supplied. Extra attributes, such as <c>lang</c>, and
/// <c>Class</c> go to the section.
/// </remarks>
public sealed partial class AlbumSection
{
    /// <summary>
    /// The default <see cref="EagerCount" />: the first row of the grid at the default content width (72rem holds four 14rem columns
    /// with their gaps).
    /// </summary>
    public const int DefaultEagerCount = 4;

    /// <summary>Gets or sets the album title, the section's heading.</summary>
    [Parameter]
    [EditorRequired]
    public string Title { get; set; } = string.Empty;

    /// <summary>Gets or sets the album's slug, which gives the heading its id <c>album-{slug}</c>.</summary>
    [Parameter]
    [EditorRequired]
    public Slug? Slug { get; set; }

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

    /// <summary>Gets or sets how many leading photos load eagerly. The default is <see cref="DefaultEagerCount" />.</summary>
    [Parameter]
    public int EagerCount { get; set; } = DefaultEagerCount;

    /// <summary>Gets or sets how the photos are arranged. The default is <see cref="ImageGridLayout.Grid" />.</summary>
    [Parameter]
    public ImageGridLayout Layout { get; set; }

    /// <summary>Gets or sets optional content after the photos and before the pager, such as a link to the related tour.</summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    private string HeadingId => "album-" + Slug!.Value;

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Title);
        ArgumentNullException.ThrowIfNull(Slug);
        ArgumentNullException.ThrowIfNull(Photos);
        ArgumentOutOfRangeException.ThrowIfNegative(EagerCount);
    }
}
