using SharedKernel.BuildingBlocks;
using SharedKernel.Results;

namespace SharedKernel.Offerings;

/// <summary>
/// A sellable programme, such as a tour: its schedule and availability, priced lines in the application's price kinds, conditions,
/// application-defined metrics, language-neutral steps, images, and translated text with a per-culture publication status. Create it with
/// <see cref="Offering.Create{TImageId, TPriceKind, TMetric}"/>, change it with <see cref="Revise"/>, store it as
/// <see cref="ToDefinition"/>, and rebuild a stored one with <see cref="Offering.Restore{TImageId, TPriceKind, TMetric}"/>.
/// </summary>
/// <typeparam name="TImageId">The image id type.</typeparam>
/// <typeparam name="TPriceKind">The application's price kind enum, stored by name.</typeparam>
/// <typeparam name="TMetric">The application's metric kind enum, stored by name.</typeparam>
/// <remarks>
/// It keeps no id, version, persistence, events or authorization: the application's own entity holds it and owns those concerns. Alias the
/// closed type once, for example <c>using TourOffering = SharedKernel.Offerings.Offering&lt;Guid, TourPriceKind, TourMetric&gt;;</c>. The
/// offering keeps the price-kind flags of the rules it was last created, revised or restored with, which decide <see cref="FromPrice"/> and
/// which prices take a label.
/// </remarks>
public sealed class Offering<TImageId, TPriceKind, TMetric>
    where TImageId : struct, IEquatable<TImageId>
    where TPriceKind : struct, Enum
    where TMetric : struct, Enum
{
    private OfferingData<TImageId, TPriceKind, TMetric> _data;

    /// <summary>Initializes an offering from validated or restored data.</summary>
    /// <param name="data">The data.</param>
    internal Offering(OfferingData<TImageId, TPriceKind, TMetric> data) => _data = data;

    /// <summary>Gets the position among the offerings, lower first.</summary>
    public int SortOrder => _data.SortOrder;

    /// <summary>Gets the schedule: availability, dates, year and duration.</summary>
    public OfferingSchedule Schedule => _data.Schedule;

    /// <summary>Gets the sign-up link; the public view shows it only for a scheduled offering (see <see cref="OfferingView{TImageId, TPriceKind, TMetric}.CallToAction"/>).</summary>
    public WebUrl? SignUpLink => _data.SignUpLink;

    /// <summary>Gets the currency of the prices and the group condition.</summary>
    public Currency? Currency => _data.Currency;

    /// <summary>Gets the prices in the rules' kind order, repeatable kinds in their entered order.</summary>
    public IReadOnlyList<PriceLine<TPriceKind>> Prices => _data.Prices;

    /// <summary>Gets the from price: the lowest price whose kind is a base kind, or <see langword="null"/> when there is none.</summary>
    public Money? FromPrice => _data.FromPrice;

    /// <summary>Gets the group sizes.</summary>
    public ParticipantRange? Participants => _data.Participants;

    /// <summary>Gets the minimum-group condition.</summary>
    public GroupCondition? GroupCondition => _data.GroupCondition;

    /// <summary>Gets the payment terms.</summary>
    public PaymentTerms? PaymentTerms => _data.PaymentTerms;

    /// <summary>Gets the offering's metrics, in the rules' order.</summary>
    public IReadOnlyList<Metric<TMetric>> Metrics => _data.Metrics;

    /// <summary>Gets the language-neutral steps of the programme, in order.</summary>
    public IReadOnlyList<ProgramStep<TImageId, TMetric>> Steps => _data.Steps;

    /// <summary>Gets the cover image.</summary>
    public TImageId? CoverId => _data.CoverId;

    /// <summary>Gets the gallery images, in order.</summary>
    public IReadOnlyList<TImageId> GalleryIds => _data.GalleryIds;

    /// <summary>Gets every image the offering shows: the cover, the gallery and the step images, each once. An application refuses to delete them.</summary>
    public IReadOnlyList<TImageId> ImageIds => _data.ImageIds;

    /// <summary>Gets the translations, in the order they were given; the order decides the same-language fallback.</summary>
    public IReadOnlyList<OfferingTranslation> Translations => _data.Translations;

    /// <summary>Determines whether the offering has a published translation in exactly this culture.</summary>
    /// <param name="culture">The culture.</param>
    /// <returns><see langword="true"/> when it has one.</returns>
    public bool IsPublishedIn(ContentCulture culture) =>
        _data.Translations.Any(translation => translation.Culture == culture && translation.Status == PublicationStatus.Published);

    /// <summary>Replaces all data after validating it, as <see cref="Offering.Create{TImageId, TPriceKind, TMetric}"/> does.</summary>
    /// <param name="definition">The entered data.</param>
    /// <param name="rules">The application's offering rules.</param>
    /// <param name="images">The application's image knowledge.</param>
    /// <returns>
    /// Whether the data changed, so the application bumps its version only then; or every validation error, with the offering unchanged.
    /// New price-kind flags of the rules are taken even when the data did not change. Store <see cref="ToDefinition"/> afterwards, never
    /// the entered definition.
    /// </returns>
    /// <exception cref="ArgumentNullException">An argument, the schedule, or an entry of a list in the definition is <see langword="null"/>.</exception>
    public Result<bool> Revise(OfferingDefinition<TImageId, TPriceKind, TMetric> definition, OfferingRules<TPriceKind, TMetric> rules, IImageEligibility<TImageId> images)
    {
        var validated = Offering.Validate(definition, rules, images);
        if (validated.IsFailure)
        {
            return validated.ConvertError<OfferingData<TImageId, TPriceKind, TMetric>, bool>();
        }

        var changed = !_data.SameAs(validated.Value);
        _data = validated.Value;
        return Result.Ok(changed);
    }

    /// <summary>
    /// Builds the public view of the offering in a culture: one whole published translation, of the requested culture, its neutral parent
    /// or another culture of its language, else of the default culture (see <see cref="TranslationSet{T}.TryResolve"/>). It never mixes the
    /// fields of two translations; <see cref="OfferingView{TImageId, TPriceKind, TMetric}.Culture"/> tells which one it is.
    /// </summary>
    /// <param name="requested">The culture of the page; <see langword="null"/> asks for the default culture.</param>
    /// <param name="cultures">The application's cultures, whose default is the fallback.</param>
    /// <returns>The view, or <see langword="null"/> when no published translation applies.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="cultures"/> is <see langword="null"/>.</exception>
    public OfferingView<TImageId, TPriceKind, TMetric>? PublishedIn(ContentCulture? requested, SupportedCultures cultures)
    {
        ArgumentNullException.ThrowIfNull(cultures);

        var published = TranslationSet.Create(
            _data.Translations.Where(translation => translation.Status == PublicationStatus.Published),
            Offering.CultureOf).Value;
        return published.TryResolve(requested, cultures.Default, out var translation)
            ? new OfferingView<TImageId, TPriceKind, TMetric>(_data, translation)
            : null;
    }

    /// <summary>
    /// Gives the offering's data as a definition that creates or restores the same offering: the only form to store in an event or a
    /// document, after <see cref="Offering.Create{TImageId, TPriceKind, TMetric}"/> or <see cref="Revise"/>; the entered definition is
    /// not sanitized, sorted or completed. A dated offering's year is left out (it comes from the dates), text with paragraphs is joined
    /// with blank lines, and a missing price label is <see langword="null"/>.
    /// </summary>
    /// <returns>The definition.</returns>
    public OfferingDefinition<TImageId, TPriceKind, TMetric> ToDefinition() => new()
    {
        SortOrder = _data.SortOrder,
        Schedule = new OfferingScheduleDefinition(
            _data.Schedule.Availability,
            _data.Schedule.Dates?.StartDate,
            _data.Schedule.Dates?.EndDate,
            _data.Schedule.Dates is null ? _data.Schedule.Year : null,
            _data.Schedule.DurationDays),
        SignUpLink = _data.SignUpLink?.Value,
        Currency = _data.Currency?.Code,
        Prices = [.. _data.Prices.Select(line => new PriceLineDefinition<TPriceKind>(line.Kind, line.Amount.Amount))],
        Participants = _data.Participants,
        GroupCondition = _data.GroupCondition is { } group
            ? new GroupConditionDefinition(group.MinimumParticipants, group.SurchargePerMissing.Amount, group.DueDays)
            : null,
        PaymentTerms = _data.PaymentTerms,
        Metrics = _data.Metrics,
        Steps = [.. _data.Steps.Select(step => new ProgramStepDefinition<TImageId, TMetric>(step.ImageId, step.Metrics))],
        CoverId = _data.CoverId,
        GalleryIds = _data.GalleryIds,
        Translations = [.. _data.Translations.Select(DefinitionOf)],
    };

    private static OfferingTranslationDefinition DefinitionOf(OfferingTranslation translation) => new()
    {
        Culture = translation.Culture.Value,
        Status = translation.Status,
        Slug = translation.Slug.Value,
        Title = translation.Title,
        Subtitle = translation.Subtitle,
        Eyebrow = translation.Eyebrow,
        Summary = translation.Summary,
        Description = Joined(translation.Description),
        DateNote = translation.DateNote,
        SeoTitle = translation.SeoTitle,
        SeoDescription = translation.SeoDescription,
        Highlights = translation.Highlights,
        Included = translation.Included,
        NotIncluded = translation.NotIncluded,
        PracticalInfo = translation.PracticalInfo,
        PriceLabels = translation.PriceLabels,
        StepTexts = [.. translation.StepTexts.Select(text => new ProgramStepTextDefinition(text.Title, Joined(text.Paragraphs), text.Note))],
    };

    private static string? Joined(IReadOnlyList<string> paragraphs) => paragraphs.Count == 0 ? null : string.Join("\n\n", paragraphs);
}
