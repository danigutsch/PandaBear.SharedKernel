using PandaBear.BuildingBlocks;
using PandaBear.Results;

namespace PandaBear.Galleries;

/// <summary>
/// A photo album: an ordered sequence of distinct photos with a cover, a sort order, an optional related item such as a tour, and a
/// title, slug and publication status per culture. Create it with <see cref="Album.Create{TPhotoId, TRelatedId}"/>, change it with
/// <see cref="Revise"/>, and rebuild a stored one with <see cref="Album.Restore{TPhotoId, TRelatedId}"/>.
/// </summary>
/// <typeparam name="TPhotoId">The photo id type.</typeparam>
/// <typeparam name="TRelatedId">The id type of the related item.</typeparam>
/// <remarks>
/// It keeps no id, version, persistence, events or authorization: the application's own entity holds it, or copies its values, and
/// owns those concerns. A translation is published only when the album has a photo and every photo is eligible in its culture (see
/// <see cref="IAlbumPhotoEligibility{TPhotoId}"/>); the rule is checked whenever the album is created or revised.
/// </remarks>
public sealed class Album<TPhotoId, TRelatedId>
    where TPhotoId : struct, IEquatable<TPhotoId>
    where TRelatedId : struct, IEquatable<TRelatedId>
{
    private AlbumData<TPhotoId, TRelatedId> _data;

    /// <summary>Initializes an album from validated or restored data.</summary>
    /// <param name="data">The data.</param>
    internal Album(AlbumData<TPhotoId, TRelatedId> data) => _data = data;

    /// <summary>Gets the position among the albums; lower first, the album id breaking ties (see <see cref="AlbumOrdering"/>).</summary>
    public int SortOrder => _data.SortOrder;

    /// <summary>Gets the optional related item, such as the tour the album shows.</summary>
    public TRelatedId? RelatedId => _data.RelatedId;

    /// <summary>Gets the photos in display order.</summary>
    public IReadOnlyList<TPhotoId> PhotoIds => _data.PhotoIds;

    /// <summary>Gets the cover the editor chose, or <see langword="null"/> when none was chosen.</summary>
    public TPhotoId? CoverId => _data.CoverId;

    /// <summary>Gets the cover shown: the chosen one, else the first photo, or <see langword="null"/> when the album has no photo.</summary>
    public TPhotoId? EffectiveCoverId => _data.CoverId ?? (_data.PhotoIds.Count > 0 ? _data.PhotoIds[0] : null);

    /// <summary>Gets the translations, in the order they were given.</summary>
    public IReadOnlyList<AlbumTranslation> Translations => _data.Translations;

    /// <summary>Determines whether the album has a published translation in exactly this culture.</summary>
    /// <param name="culture">The culture.</param>
    /// <returns><see langword="true"/> when it has one.</returns>
    public bool IsPublishedIn(ContentCulture culture) =>
        _data.Translations.Any(translation => translation.Culture == culture && translation.Status == PublicationStatus.Published);

    /// <summary>Replaces all data after validating it, as <see cref="Album.Create{TPhotoId, TRelatedId}"/> does.</summary>
    /// <param name="definition">The entered data.</param>
    /// <param name="rules">The application's album rules.</param>
    /// <param name="photos">The application's photo knowledge.</param>
    /// <returns>
    /// Whether anything changed, so the application bumps its version only then; or every validation error, with the album unchanged.
    /// The order of the translations counts: it decides which translation of a language the public view falls back to.
    /// </returns>
    /// <exception cref="ArgumentNullException">An argument, or a translation in the definition, is <see langword="null"/>.</exception>
    public Result<bool> Revise(AlbumDefinition<TPhotoId, TRelatedId> definition, AlbumRules rules, IAlbumPhotoEligibility<TPhotoId> photos)
    {
        var validated = AlbumValidation.Validate(definition, rules, photos);
        if (validated.IsFailure)
        {
            return validated.ConvertError<AlbumData<TPhotoId, TRelatedId>, bool>();
        }

        if (_data.SameAs(validated.Value))
        {
            return Result.Ok(false);
        }

        _data = validated.Value;
        return Result.Ok(true);
    }

    /// <summary>
    /// Builds the public view of the album in a culture: the published translation of the requested culture, its neutral parent or
    /// another culture of its language, else that of the default culture (see <see cref="TranslationSet{T}.TryResolve"/>). The view's
    /// <see cref="AlbumView{TPhotoId, TRelatedId}.Culture"/> tells which one, so the page can mark text of another language.
    /// </summary>
    /// <param name="requested">The culture of the page; <see langword="null"/> asks for the default culture.</param>
    /// <param name="cultures">The application's cultures, whose default is the fallback.</param>
    /// <returns>The view, or <see langword="null"/> when no published translation applies.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="cultures"/> is <see langword="null"/>.</exception>
    public AlbumView<TPhotoId, TRelatedId>? PublishedIn(ContentCulture? requested, SupportedCultures cultures)
    {
        ArgumentNullException.ThrowIfNull(cultures);

        var published = TranslationSet.Create(
            _data.Translations.Where(translation => translation.Status == PublicationStatus.Published),
            Album.CultureOf).Value;
        return published.TryResolve(requested, cultures.Default, out var translation)
            ? new AlbumView<TPhotoId, TRelatedId>(translation, _data.SortOrder, _data.RelatedId, _data.PhotoIds, EffectiveCoverId)
            : null;
    }
}
