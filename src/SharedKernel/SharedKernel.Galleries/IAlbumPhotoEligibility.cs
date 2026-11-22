using PandaBear.BuildingBlocks;

namespace PandaBear.Galleries;

/// <summary>
/// The application's knowledge of its photos, which the album rules read: whether a photo exists, and whether it may be published in a
/// culture. One application asks for alt text, or a decorative mark, in that culture; another asks that the photo is processed and its
/// text reviewed.
/// </summary>
/// <typeparam name="TPhotoId">The photo id type.</typeparam>
public interface IAlbumPhotoEligibility<in TPhotoId>
{
    /// <summary>Determines whether a photo exists.</summary>
    /// <param name="photoId">The photo.</param>
    /// <returns><see langword="true"/> when it exists.</returns>
    bool Exists(TPhotoId photoId);

    /// <summary>Determines whether a photo may be shown in an album published in a culture.</summary>
    /// <param name="photoId">The photo.</param>
    /// <param name="culture">The culture of the published translation.</param>
    /// <returns><see langword="true"/> when it may.</returns>
    bool IsEligible(TPhotoId photoId, ContentCulture culture);
}
