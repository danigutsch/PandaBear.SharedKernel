namespace PandaBear.Offerings;

/// <summary>
/// Unvalidated offering data, as an editor entered it, and the round-trip form of a stored offering (see
/// <see cref="Offering{TImageId, TPriceKind, TMetric}.ToDefinition"/>): an event-sourced application stores it in its event and replays it
/// with <see cref="Offering.Restore{TImageId, TPriceKind, TMetric}"/>. Lists that are <see langword="null"/> are empty.
/// </summary>
/// <typeparam name="TImageId">The image id type.</typeparam>
/// <typeparam name="TPriceKind">The application's price kind enum.</typeparam>
/// <typeparam name="TMetric">The application's metric kind enum.</typeparam>
public sealed record OfferingDefinition<TImageId, TPriceKind, TMetric>
    where TImageId : struct, IEquatable<TImageId>
    where TPriceKind : struct, Enum
    where TMetric : struct, Enum
{
    /// <summary>Gets the position among the offerings, lower first.</summary>
    public int SortOrder { get; init; }

    /// <summary>Gets the schedule.</summary>
    public required OfferingScheduleDefinition Schedule { get; init; }

    /// <summary>Gets the optional http or https sign-up address; shown only while the offering is scheduled.</summary>
    public string? SignUpLink { get; init; }

    /// <summary>Gets the ISO 4217 currency code; required with prices or a group condition.</summary>
    public string? Currency { get; init; }

    /// <summary>Gets the prices; they are sorted into the rules' kind order, repeatable kinds keeping their entered order.</summary>
    public IReadOnlyList<PriceLineDefinition<TPriceKind>>? Prices { get; init; }

    /// <summary>Gets the optional group sizes.</summary>
    public ParticipantRange? Participants { get; init; }

    /// <summary>Gets the optional group condition.</summary>
    public GroupConditionDefinition? GroupCondition { get; init; }

    /// <summary>Gets the optional payment terms.</summary>
    public PaymentTerms? PaymentTerms { get; init; }

    /// <summary>Gets the offering's metrics; they are sorted into the rules' order.</summary>
    public IReadOnlyList<Metric<TMetric>>? Metrics { get; init; }

    /// <summary>Gets the language-neutral steps of the programme, in order.</summary>
    public IReadOnlyList<ProgramStepDefinition<TImageId, TMetric>>? Steps { get; init; }

    /// <summary>Gets the optional cover image.</summary>
    public TImageId? CoverId { get; init; }

    /// <summary>Gets the gallery images in order, each once.</summary>
    public IReadOnlyList<TImageId>? GalleryIds { get; init; }

    /// <summary>Gets the text per culture; the order decides the same-language fallback of the public view.</summary>
    public IReadOnlyList<OfferingTranslationDefinition>? Translations { get; init; }
}
