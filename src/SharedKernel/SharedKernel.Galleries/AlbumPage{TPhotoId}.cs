using PandaBear.BuildingBlocks;

namespace PandaBear.Galleries;

/// <summary>One page of an album's photos: the page window and the photos on it.</summary>
/// <typeparam name="TPhotoId">The photo id type.</typeparam>
public sealed class AlbumPage<TPhotoId>
{
    /// <summary>Initializes the page.</summary>
    /// <param name="window">The page window.</param>
    /// <param name="photoIds">The photos on the page.</param>
    internal AlbumPage(PageWindow window, IReadOnlyList<TPhotoId> photoIds)
    {
        Window = window;
        PhotoIds = photoIds;
    }

    /// <summary>Gets the page window, for the pager and for <see cref="PageWindow.IsOutOfRange"/>.</summary>
    public PageWindow Window { get; }

    /// <summary>Gets the photos on the page, in display order.</summary>
    public IReadOnlyList<TPhotoId> PhotoIds { get; }
}
