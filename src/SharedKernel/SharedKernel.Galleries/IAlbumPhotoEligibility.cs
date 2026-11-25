using SharedKernel.BuildingBlocks;

namespace SharedKernel.Galleries;

/// <summary>
/// The application's knowledge of its photos, which the album rules read: whether a photo exists, and whether it may be published in a
/// culture (see <see cref="IImageEligibility{TImageId}"/>). One application asks for alt text, or a decorative mark, in that culture;
/// another asks that the photo is processed and its text reviewed. An application with one image library implements it once for albums
/// and every other content that shows images.
/// </summary>
/// <typeparam name="TPhotoId">The photo id type.</typeparam>
public interface IAlbumPhotoEligibility<in TPhotoId> : IImageEligibility<TPhotoId>;
