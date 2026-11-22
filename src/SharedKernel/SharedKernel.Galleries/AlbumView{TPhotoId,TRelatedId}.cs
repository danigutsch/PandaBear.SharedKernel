using PandaBear.BuildingBlocks;

namespace PandaBear.Galleries;

/// <summary>The public view of a published album in one culture, built by <see cref="Album{TPhotoId, TRelatedId}.PublishedIn"/>.</summary>
/// <typeparam name="TPhotoId">The photo id type.</typeparam>
/// <typeparam name="TRelatedId">The id type of the related item.</typeparam>
public sealed class AlbumView<TPhotoId, TRelatedId>
    where TPhotoId : struct, IEquatable<TPhotoId>
    where TRelatedId : struct, IEquatable<TRelatedId>
{
    private readonly AlbumTranslation _translation;

    /// <summary>Initializes the view.</summary>
    /// <param name="translation">The resolved published translation.</param>
    /// <param name="sortOrder">The sort order.</param>
    /// <param name="relatedId">The related item.</param>
    /// <param name="photoIds">The photos.</param>
    /// <param name="coverId">The cover shown.</param>
    internal AlbumView(AlbumTranslation translation, int sortOrder, TRelatedId? relatedId, IReadOnlyList<TPhotoId> photoIds, TPhotoId? coverId)
    {
        _translation = translation;
        SortOrder = sortOrder;
        RelatedId = relatedId;
        PhotoIds = photoIds;
        CoverId = coverId;
    }

    /// <summary>Gets the culture of the text shown, which differs from the requested one when the album fell back.</summary>
    public ContentCulture Culture => _translation.Culture;

    /// <summary>Gets the title.</summary>
    public string Title => _translation.Title;

    /// <summary>Gets the slug of the album's address.</summary>
    public Slug Slug => _translation.Slug;

    /// <summary>Gets the sort order.</summary>
    public int SortOrder { get; }

    /// <summary>Gets the related item.</summary>
    public TRelatedId? RelatedId { get; }

    /// <summary>Gets the photos in display order.</summary>
    public IReadOnlyList<TPhotoId> PhotoIds { get; }

    /// <summary>Gets the number of photos.</summary>
    public int PhotoCount => PhotoIds.Count;

    /// <summary>Gets the cover shown: the chosen one, else the first photo.</summary>
    public TPhotoId? CoverId { get; }

    /// <summary>Builds one page of photos from the page number in the URL.</summary>
    /// <param name="requestedPage">The query string value; see <see cref="PageWindow.Create(string?, int, int)"/>.</param>
    /// <param name="pageSize">The most photos on a page.</param>
    /// <returns>The page; answer 404 Not Found when its <see cref="PageWindow.IsOutOfRange"/> is <see langword="true"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="pageSize"/> is zero or negative.</exception>
    public AlbumPage<TPhotoId> Page(string? requestedPage, int pageSize) => Slice(PageWindow.Create(requestedPage, pageSize, PhotoIds.Count));

    /// <summary>Builds one page of photos from a page number.</summary>
    /// <param name="requestedPage">The page; see <see cref="PageWindow.Create(int?, int, int)"/>.</param>
    /// <param name="pageSize">The most photos on a page.</param>
    /// <returns>The page.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="pageSize"/> is zero or negative.</exception>
    public AlbumPage<TPhotoId> Page(int? requestedPage, int pageSize) => Slice(PageWindow.Create(requestedPage, pageSize, PhotoIds.Count));

    private AlbumPage<TPhotoId> Slice(PageWindow window) => new(window, [.. PhotoIds.Skip(window.Skip).Take(window.Take)]);
}
