namespace SharedKernel.Blazor.Site;

/// <summary>One album of an <see cref="AlbumIndex" />: what its card shows.</summary>
/// <param name="Title">The album title, the card's heading.</param>
/// <param name="Href">The address of the album's page, which the heading links to.</param>
/// <param name="Cover">The optional cover, loaded lazily; usually decorative, because the title names the card.</param>
/// <param name="PhotoCountText">The optional photo count as the application words it, for example "12 photos".</param>
/// <param name="TitleLanguage">
/// The BCP 47 language tag of the title when it is in another language than the page, such as <c>en</c> when the album fell back to
/// English on a Portuguese page; written as <c>lang</c> on the card's heading alone. The application maps its content culture to the tag.
/// </param>
public sealed record AlbumSummary(
    string Title,
    LinkTarget Href,
    ResponsiveImage? Cover = null,
    string? PhotoCountText = null,
    string? TitleLanguage = null)
{
    /// <summary>Gets the optional short credit shown with <see cref="Cover" />.</summary>
    public ImageCreditLine? CoverCredit { get; init; }
}
