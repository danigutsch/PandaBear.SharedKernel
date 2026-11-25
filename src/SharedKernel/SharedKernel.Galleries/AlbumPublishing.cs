using SharedKernel.BuildingBlocks;

namespace SharedKernel.Galleries;

/// <summary>Reads the publish rule of albums.</summary>
public static class AlbumPublishing
{
    /// <summary>
    /// Lists the photos that keep an album from being published in a culture: those that do not exist or are not eligible there, each
    /// once, in album order. An editor can name them; the validation error only counts them.
    /// </summary>
    /// <typeparam name="TPhotoId">The photo id type.</typeparam>
    /// <param name="photoIds">The album's photos.</param>
    /// <param name="culture">The culture to publish in.</param>
    /// <param name="photos">The application's photo knowledge.</param>
    /// <returns>The photos that are not eligible.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static IReadOnlyList<TPhotoId> FindIneligible<TPhotoId>(IEnumerable<TPhotoId> photoIds, ContentCulture culture, IAlbumPhotoEligibility<TPhotoId> photos)
        where TPhotoId : struct, IEquatable<TPhotoId>
    {
        ArgumentNullException.ThrowIfNull(photoIds);
        ArgumentNullException.ThrowIfNull(culture);
        ArgumentNullException.ThrowIfNull(photos);

        return [.. photoIds.Distinct().Where(photoId => !photos.Exists(photoId) || !photos.IsEligible(photoId, culture))];
    }
}
