using PandaBear.BuildingBlocks;

namespace PandaBear.Offerings;

/// <summary>The validated or restored data of an offering, with the price-kind flags of the rules it was built with.</summary>
/// <typeparam name="TImageId">The image id type.</typeparam>
/// <typeparam name="TPriceKind">The application's price kind enum.</typeparam>
/// <typeparam name="TMetric">The application's metric kind enum.</typeparam>
internal sealed record OfferingData<TImageId, TPriceKind, TMetric>
    where TImageId : struct, IEquatable<TImageId>
    where TPriceKind : struct, Enum
    where TMetric : struct, Enum
{
    /// <summary>Gets the sort order.</summary>
    public required int SortOrder { get; init; }

    /// <summary>Gets the schedule.</summary>
    public required OfferingSchedule Schedule { get; init; }

    /// <summary>Gets the sign-up link.</summary>
    public required WebUrl? SignUpLink { get; init; }

    /// <summary>Gets the currency.</summary>
    public required Currency? Currency { get; init; }

    /// <summary>Gets the prices in display order.</summary>
    public required IReadOnlyList<PriceLine<TPriceKind>> Prices { get; init; }

    /// <summary>Gets the group sizes.</summary>
    public required ParticipantRange? Participants { get; init; }

    /// <summary>Gets the group condition.</summary>
    public required GroupCondition? GroupCondition { get; init; }

    /// <summary>Gets the payment terms.</summary>
    public required PaymentTerms? PaymentTerms { get; init; }

    /// <summary>Gets the offering's metrics.</summary>
    public required IReadOnlyList<Metric<TMetric>> Metrics { get; init; }

    /// <summary>Gets the steps.</summary>
    public required IReadOnlyList<ProgramStep<TImageId, TMetric>> Steps { get; init; }

    /// <summary>Gets the cover.</summary>
    public required TImageId? CoverId { get; init; }

    /// <summary>Gets the gallery.</summary>
    public required IReadOnlyList<TImageId> GalleryIds { get; init; }

    /// <summary>Gets the translations.</summary>
    public required IReadOnlyList<OfferingTranslation> Translations { get; init; }

    /// <summary>Gets the price kinds of the rules the data was built with; they decide the from price and the labelled prices.</summary>
    public required IReadOnlyList<PriceKindRule<TPriceKind>> PriceKinds { get; init; }

    /// <summary>Gets every image: the cover, the gallery and the step images, each once, in that order.</summary>
    public IReadOnlyList<TImageId> ImageIds =>
        [.. (CoverId is { } cover ? GalleryIds.Prepend(cover) : GalleryIds)
            .Concat(Steps.Where(step => step.ImageId.HasValue).Select(step => step.ImageId!.Value))
            .Distinct()];

    /// <summary>Gets the from price: the lowest price whose kind is a base kind, or <see langword="null"/> when there is none.</summary>
    public Money? FromPrice => Prices.Where(line => KindOf(line.Kind)?.IsBase == true).MinBy(line => line.Amount.Amount)?.Amount;

    /// <summary>Finds the rule of a price kind among the stored flags.</summary>
    /// <param name="kind">The kind.</param>
    /// <returns>The rule, or <see langword="null"/> when the rules no longer list the kind.</returns>
    public PriceKindRule<TPriceKind>? KindOf(TPriceKind kind) =>
        PriceKinds.FirstOrDefault(rule => EqualityComparer<TPriceKind>.Default.Equals(rule.Kind, kind));

    /// <summary>
    /// Determines whether other data is the same, ignoring the price-kind flags, which belong to the rules. Translations are compared in
    /// order, because the same-language fallback of <see cref="TranslationSet{T}"/> picks the first matching translation.
    /// </summary>
    /// <param name="other">The other data.</param>
    /// <returns><see langword="true"/> when nothing differs.</returns>
    public bool SameAs(OfferingData<TImageId, TPriceKind, TMetric> other) =>
        SortOrder == other.SortOrder
        && Schedule == other.Schedule
        && SignUpLink == other.SignUpLink
        && Currency == other.Currency
        && Prices.SequenceEqual(other.Prices)
        && Participants == other.Participants
        && GroupCondition == other.GroupCondition
        && PaymentTerms == other.PaymentTerms
        && Metrics.SequenceEqual(other.Metrics)
        && Steps.Count == other.Steps.Count
        && Steps.Zip(other.Steps).All(pair => pair.First.SameAs(pair.Second))
        && Nullable.Equals(CoverId, other.CoverId)
        && GalleryIds.SequenceEqual(other.GalleryIds)
        && Translations.Count == other.Translations.Count
        && Translations.Zip(other.Translations).All(pair => pair.First.SameAs(pair.Second));
}
