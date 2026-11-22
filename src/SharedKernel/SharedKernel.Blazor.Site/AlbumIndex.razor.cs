using Microsoft.AspNetCore.Components;

namespace PandaBear.Blazor.Site;

/// <summary>
/// The index of a site's photo albums: a <see cref="CardGrid" /> with one <see cref="Card" /> per album, whose heading links to the
/// album's page, with its cover and the photo count as the application words it. The covers of the first <see cref="EagerCount" /> albums
/// load eagerly, because the first row of an index is usually on the first screen, and the rest lazily.
/// </summary>
/// <remarks>
/// Nothing is rendered without albums, so the page shows its own empty message. All text comes from the <see cref="AlbumSummary" />
/// records. Extra attributes, such as an <c>aria-label</c>, and <c>Class</c> go to the grid.
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
