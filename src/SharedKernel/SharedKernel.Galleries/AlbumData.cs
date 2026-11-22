namespace PandaBear.Galleries;

/// <summary>The validated data of an album.</summary>
/// <typeparam name="TPhotoId">The photo id type.</typeparam>
/// <typeparam name="TRelatedId">The id type of the related item.</typeparam>
/// <param name="SortOrder">The sort order.</param>
/// <param name="RelatedId">The related item.</param>
/// <param name="PhotoIds">The photos in display order.</param>
/// <param name="CoverId">The chosen cover.</param>
/// <param name="Translations">The translations.</param>
internal sealed record AlbumData<TPhotoId, TRelatedId>(
    int SortOrder,
    TRelatedId? RelatedId,
    IReadOnlyList<TPhotoId> PhotoIds,
    TPhotoId? CoverId,
    IReadOnlyList<AlbumTranslation> Translations)
    where TPhotoId : struct, IEquatable<TPhotoId>
    where TRelatedId : struct, IEquatable<TRelatedId>
{
    /// <summary>
    /// Determines whether other data is the same. Translations are compared in order, because the same-language fallback of
    /// <see cref="PandaBear.BuildingBlocks.TranslationSet{T}"/> picks the first matching translation.
    /// </summary>
    /// <param name="other">The other data.</param>
    /// <returns><see langword="true"/> when nothing differs.</returns>
    public bool SameAs(AlbumData<TPhotoId, TRelatedId> other) =>
        SortOrder == other.SortOrder
        && Nullable.Equals(RelatedId, other.RelatedId)
        && Nullable.Equals(CoverId, other.CoverId)
        && PhotoIds.SequenceEqual(other.PhotoIds)
        && Translations.Count == other.Translations.Count
        && Translations.Zip(other.Translations).All(pair => pair.First.SameAs(pair.Second));
}
