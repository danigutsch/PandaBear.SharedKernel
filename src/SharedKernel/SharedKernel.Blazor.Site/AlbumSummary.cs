using PandaBear.BuildingBlocks;

namespace PandaBear.Blazor.Site;

/// <summary>One album of an <see cref="AlbumIndex" />: what its card shows.</summary>
/// <param name="Title">The album title, the card's heading.</param>
/// <param name="Href">The address of the album's page, which the heading links to.</param>
/// <param name="Cover">The optional cover, loaded lazily; usually decorative, because the title names the card.</param>
/// <param name="PhotoCountText">The optional photo count as the application words it, for example "12 photos".</param>
/// <param name="Culture">The culture of the title when it differs from the page's, written as the card's <c>lang</c>.</param>
public sealed record AlbumSummary(
    string Title,
    LinkTarget Href,
    ResponsiveImage? Cover = null,
    string? PhotoCountText = null,
    ContentCulture? Culture = null);
