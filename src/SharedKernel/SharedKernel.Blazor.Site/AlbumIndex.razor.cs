using Microsoft.AspNetCore.Components;

namespace PandaBear.Blazor.Site;

/// <summary>
/// The index of a site's photo albums: a <see cref="CardGrid" /> with one <see cref="Card" /> per album, whose heading links to the
/// album's page, with its cover and the photo count as the application words it. The covers of the first <see cref="EagerCount" /> albums
/// load eagerly, because the first row of an index is usually on the first screen, and the rest lazily.
/// </summary>
/// <remarks>
/// Nothing is rendered without albums, so the page shows its own empty message. All text comes from the <see cref="AlbumSummary" />
/// records. Extra attributes, such as an <c>aria-label</c>, and <c>Class</c> go to the grid; the card and grid options go to every card
/// and to the grid.
/// </remarks>
public sealed partial class AlbumIndex
{
    /// <summary>
    /// The default <see cref="EagerCount" />: the first row of the grid at the default content width (72rem holds four 16rem card columns
    /// with their gaps).
    /// </summary>
    public const int DefaultEagerCount = 4;

    /// <summary>Gets or sets the albums, in display order.</summary>
    [Parameter]
    [EditorRequired]
    public IReadOnlyList<AlbumSummary> Albums { get; set; } = [];

    /// <summary>Gets or sets the heading level of each card, from 1 to 6. The default is 3.</summary>
    [Parameter]
    public int HeadingLevel { get; set; } = 3;

    /// <summary>
    /// Gets or sets how many leading covers load eagerly. The default is <see cref="DefaultEagerCount" />; lower it when a hero or text
    /// comes before the index, and use 0 for an index below the first screen.
    /// </summary>
    [Parameter]
    public int EagerCount { get; set; } = DefaultEagerCount;

    /// <summary>Gets or sets the look of every album card (<see cref="Card.CardStyle" />). The default is <see cref="CardStyle.Boxed" />.</summary>
    [Parameter]
    public CardStyle CardStyle { get; set; }

    /// <summary>Gets or sets the aspect ratio of every cover (<see cref="Card.ImageAspect" />). The default is <see cref="CardImageAspect.FourByThree" />.</summary>
    [Parameter]
    public CardImageAspect ImageAspect { get; set; }

    /// <summary>Gets or sets the decoration behind every cover (<see cref="Card.ImageShadow" />); an offset shadow needs plain cards.</summary>
    [Parameter]
    public CardImageShadow ImageShadow { get; set; }

    /// <summary>Gets or sets the text alignment of every card (<see cref="Card.TextAlignment" />). The default is <see cref="SiteAlignment.Start" />.</summary>
    [Parameter]
    public SiteAlignment TextAlignment { get; set; }

    /// <summary>Gets or sets the more look of every card (<see cref="Card.MoreText" />), such as "See the photos"; none when null or blank.</summary>
    [Parameter]
    public string? MoreText { get; set; }

    /// <summary>Gets or sets the variant of the more look (<see cref="Card.MoreVariant" />). The default is <see cref="ButtonVariant.Outline" />.</summary>
    [Parameter]
    public ButtonVariant MoreVariant { get; set; } = ButtonVariant.Outline;

    /// <summary>Gets or sets the size of the more look (<see cref="Card.MoreSize" />). The default is <see cref="ButtonSize.Regular" />.</summary>
    [Parameter]
    public ButtonSize MoreSize { get; set; }

    /// <summary>Gets or sets the smallest column width of the grid (<see cref="CardGrid.MinColumnWidth" />).</summary>
    [Parameter]
    public CardColumnWidth MinColumnWidth { get; set; }

    /// <summary>Gets or sets the largest column width of the grid (<see cref="CardGrid.MaxColumnWidth" />); the columns share the row when null.</summary>
    [Parameter]
    public CardColumnWidth? MaxColumnWidth { get; set; }

    /// <summary>Gets or sets the space between the cards (<see cref="CardGrid.Gap" />). The default is <see cref="SiteSpace.Space4" />.</summary>
    [Parameter]
    public SiteSpace Gap { get; set; } = SiteSpace.Space4;

    /// <summary>Gets or sets where capped cards lie in their columns (<see cref="CardGrid.ItemAlignment" />). The default is <see cref="SiteAlignment.Start" />.</summary>
    [Parameter]
    public SiteAlignment ItemAlignment { get; set; }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        ArgumentNullException.ThrowIfNull(Albums);
        ArgumentOutOfRangeException.ThrowIfNegative(EagerCount);
        foreach (var album in Albums)
        {
            ArgumentNullException.ThrowIfNull(album, nameof(Albums));
            ArgumentNullException.ThrowIfNull(album.Href, nameof(Albums));
        }
    }
}
