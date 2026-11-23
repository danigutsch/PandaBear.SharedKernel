using System.Globalization;
using PandaBear.BuildingBlocks;
using PandaBear.Results;

namespace PandaBear.Galleries;

/// <summary>Validates album definitions into album data. Messages never echo the input.</summary>
internal static class AlbumValidation
{
    /// <summary>Validates a definition.</summary>
    /// <typeparam name="TPhotoId">The photo id type.</typeparam>
    /// <typeparam name="TRelatedId">The id type of the related item.</typeparam>
    /// <param name="definition">The entered data.</param>
    /// <param name="rules">The rules.</param>
    /// <param name="photos">The photo knowledge.</param>
    /// <returns>The validated data, or every error.</returns>
    /// <exception cref="ArgumentNullException">An argument, or a translation in the definition, is <see langword="null"/>.</exception>
    public static Result<AlbumData<TPhotoId, TRelatedId>> Validate<TPhotoId, TRelatedId>(
        AlbumDefinition<TPhotoId, TRelatedId> definition,
        AlbumRules rules,
        IAlbumPhotoEligibility<TPhotoId> photos)
        where TPhotoId : struct, IEquatable<TPhotoId>
        where TRelatedId : struct, IEquatable<TRelatedId>
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(photos);

        var errors = new ValidationErrors();
        var photoIds = definition.PhotoIds ?? [];
        if (photoIds.Count > rules.MaxPhotos)
        {
            Add(errors, AlbumFields.PhotoIds, string.Create(CultureInfo.InvariantCulture, $"An album cannot hold more than {rules.MaxPhotos} photos."));
        }
        else if (photoIds.Any(photoId => !photos.Exists(photoId)))
        {
            Add(errors, AlbumFields.PhotoIds, "A photo does not exist.");
        }
        else if (photoIds.Distinct().Count() != photoIds.Count)
        {
            Add(errors, AlbumFields.PhotoIds, "A photo appears twice.");
        }

        if (definition.CoverId is { } cover && !photoIds.Contains(cover))
        {
            Add(errors, AlbumFields.CoverId, "The cover must be one of the album's photos.");
        }

        var translations = new TranslationListRules(rules.Cultures, AlbumFields.Translations, "album").Validate(
            errors,
            definition.Translations,
            static translation => translation.Culture,
            (culture, translation) => Translation(errors, rules, culture, translation, photoIds, photos),
            nameof(definition));

        return errors.HasErrors
            ? errors.ToResult<AlbumData<TPhotoId, TRelatedId>>()
            : Result.Ok(new AlbumData<TPhotoId, TRelatedId>(definition.SortOrder, definition.RelatedId, [.. photoIds], definition.CoverId, [.. translations]));
    }

    private static AlbumTranslation? Translation<TPhotoId>(
        ValidationErrors errors,
        AlbumRules rules,
        ContentCulture culture,
        AlbumTranslationDefinition definition,
        IReadOnlyList<TPhotoId> photoIds,
        IAlbumPhotoEligibility<TPhotoId> photos)
        where TPhotoId : struct, IEquatable<TPhotoId>
    {
        var title = SanitizedText.Required(errors, definition.Title, AlbumFields.Of(culture, AlbumFields.Title), "Title", rules.MaxTitleLength);
        var valid = title is not null;
        var slug = SlugOf(errors, rules, culture, definition.Slug, title);
        valid &= slug is not null;

        if (!Enum.IsDefined(definition.Status))
        {
            valid = Add(errors, AlbumFields.Of(culture, AlbumFields.Status), "The publication status is not valid.");
        }
        else if (definition.Status == PublicationStatus.Published)
        {
            valid &= PublishRule(errors, culture, photoIds, photos);
        }

        return valid ? AlbumTranslation.Restore(culture, title!, slug!, definition.Status) : null;
    }

    private static Slug? SlugOf(ValidationErrors errors, AlbumRules rules, ContentCulture culture, string? value, string? title)
    {
        var field = AlbumFields.Of(culture, AlbumFields.Slug);
        if (string.IsNullOrWhiteSpace(value) && title is null)
        {
            return null;
        }

        var slug = string.IsNullOrWhiteSpace(value)
            ? Slug.CreateLenient(title, rules.MaxSlugLength, field)
            : Slug.Create(value, rules.MaxSlugLength, field);
        if (slug.IsSuccess)
        {
            return slug.Value;
        }

        errors.Add(slug);
        return null;
    }

    private static bool PublishRule<TPhotoId>(ValidationErrors errors, ContentCulture culture, IReadOnlyList<TPhotoId> photoIds, IAlbumPhotoEligibility<TPhotoId> photos)
        where TPhotoId : struct, IEquatable<TPhotoId>
    {
        var field = AlbumFields.Of(culture, AlbumFields.Photos);
        if (photoIds.Count == 0)
        {
            return Add(errors, field, "An album needs at least one photo before it is published.");
        }

        var ineligible = AlbumPublishing.FindIneligible(photoIds, culture, photos).Count;
        return ineligible == 0
            || Add(errors, field, string.Create(CultureInfo.InvariantCulture, $"Every photo must be eligible in this language before publishing; {ineligible} are not."));
    }

    private static bool Add(ValidationErrors errors, string field, string message)
    {
        errors.Add(Result.Invalid(detail: message, field: field, message: message));
        return false;
    }
}
