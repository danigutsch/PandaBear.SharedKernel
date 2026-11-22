using Microsoft.AspNetCore.Components;

namespace PandaBear.Blazor.Site;

/// <summary>
/// The index of a site's photo albums: a <see cref="CardGrid" /> with one <see cref="Card" /> per album, whose heading links to the
/// album's page, with its cover loaded lazily and the photo count as the application words it.
/// </summary>
/// <remarks>
/// Nothing is rendered without albums, so the page shows its own empty message. All text comes from the <see cref="AlbumSummary" />
/// records. Extra attributes, such as an <c>aria-label</c>, and <c>Class</c> go to the grid.
/// </remarks>
public sealed partial class AlbumIndex
{
    /// <summary>Gets or sets the albums, in display order.</summary>
    [Parameter]
    [EditorRequired]
    public IReadOnlyList<AlbumSummary> Albums { get; set; } = [];

    /// <summary>Gets or sets the heading level of each card, from 1 to 6. The default is 3.</summary>
    [Parameter]
    public int HeadingLevel { get; set; } = 3;

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        ArgumentNullException.ThrowIfNull(Albums);
        foreach (var album in Albums)
        {
            ArgumentNullException.ThrowIfNull(album, nameof(Albums));
        }
    }
}
