namespace SharedKernel.Galleries;

/// <summary>Unvalidated album data, as an editor entered it.</summary>
/// <typeparam name="TPhotoId">The photo id type.</typeparam>
/// <typeparam name="TRelatedId">The id type of the related item, such as a tour.</typeparam>
/// <param name="SortOrder">The position among the albums; lower first, the album id breaking ties.</param>
/// <param name="RelatedId">The optional related item, such as the tour the album shows.</param>
/// <param name="PhotoIds">The photos in display order; <see langword="null"/> is none.</param>
/// <param name="CoverId">The cover, one of the photos; <see langword="null"/> makes the first photo the cover.</param>
/// <param name="Translations">The text per culture; <see langword="null"/> is none.</param>
public sealed record AlbumDefinition<TPhotoId, TRelatedId>(
    int SortOrder,
    TRelatedId? RelatedId,
    IReadOnlyList<TPhotoId>? PhotoIds,
    TPhotoId? CoverId,
    IReadOnlyList<AlbumTranslationDefinition>? Translations)
    where TPhotoId : struct, IEquatable<TPhotoId>
    where TRelatedId : struct, IEquatable<TRelatedId>;
