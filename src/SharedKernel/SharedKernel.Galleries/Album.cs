using PandaBear.BuildingBlocks;
using PandaBear.Results;

namespace PandaBear.Galleries;

/// <summary>Creates and restores <see cref="Album{TPhotoId, TRelatedId}"/> values.</summary>
public static class Album
{
    /// <summary>Creates an album after validating every field and the publish rule of each published translation.</summary>
    /// <typeparam name="TPhotoId">The photo id type.</typeparam>
    /// <typeparam name="TRelatedId">The id type of the related item.</typeparam>
    /// <param name="definition">The entered data.</param>
    /// <param name="rules">The application's album rules.</param>
    /// <param name="photos">The application's photo knowledge.</param>
    /// <returns>The album, or every validation error with its field path (see <see cref="AlbumFields"/>).</returns>
    /// <exception cref="ArgumentNullException">An argument, or a translation in the definition, is <see langword="null"/>.</exception>
    public static Result<Album<TPhotoId, TRelatedId>> Create<TPhotoId, TRelatedId>(
        AlbumDefinition<TPhotoId, TRelatedId> definition,
        AlbumRules rules,
        IAlbumPhotoEligibility<TPhotoId> photos)
        where TPhotoId : struct, IEquatable<TPhotoId>
        where TRelatedId : struct, IEquatable<TRelatedId>
    {
        var validated = AlbumValidation.Validate(definition, rules, photos);
        return validated.IsSuccess
            ? Result.Ok(new Album<TPhotoId, TRelatedId>(validated.Value))
            : validated.ConvertError<AlbumData<TPhotoId, TRelatedId>, Album<TPhotoId, TRelatedId>>();
    }

    /// <summary>For persistence mappers: rebuilds an album stored earlier; it performs no validation beyond null checks, so never use it for input.</summary>
    /// <typeparam name="TPhotoId">The photo id type.</typeparam>
    /// <typeparam name="TRelatedId">The id type of the related item.</typeparam>
    /// <param name="sortOrder">The sort order.</param>
    /// <param name="relatedId">The related item.</param>
    /// <param name="photoIds">The photos in display order.</param>
    /// <param name="coverId">The chosen cover.</param>
    /// <param name="translations">The translations.</param>
    /// <returns>The album.</returns>
    /// <exception cref="ArgumentNullException">A list, or a translation in it, is <see langword="null"/>.</exception>
    public static Album<TPhotoId, TRelatedId> Restore<TPhotoId, TRelatedId>(
        int sortOrder,
        TRelatedId? relatedId,
        IReadOnlyList<TPhotoId> photoIds,
        TPhotoId? coverId,
        IReadOnlyList<AlbumTranslation> translations)
        where TPhotoId : struct, IEquatable<TPhotoId>
        where TRelatedId : struct, IEquatable<TRelatedId>
    {
        ArgumentNullException.ThrowIfNull(photoIds);
        ArgumentNullException.ThrowIfNull(translations);
        foreach (var translation in translations)
        {
            ArgumentNullException.ThrowIfNull(translation, nameof(translations));
        }

        return new Album<TPhotoId, TRelatedId>(new AlbumData<TPhotoId, TRelatedId>(sortOrder, relatedId, [.. photoIds], coverId, [.. translations]));
    }

    /// <summary>Reads the culture of a translation; used to build <see cref="TranslationSet{T}"/> values.</summary>
    /// <param name="translation">The translation.</param>
    /// <returns>Its culture.</returns>
    internal static ContentCulture CultureOf(AlbumTranslation translation) => translation.Culture;
}
