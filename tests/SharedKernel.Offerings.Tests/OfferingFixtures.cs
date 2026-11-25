using SharedKernel.Results;

namespace SharedKernel.Offerings.Tests;

/// <summary>Cultures, rules and definitions shared by the offering tests, shaped like ViajantesTurismo V2's tours.</summary>
internal static class OfferingFixtures
{
    /// <summary>Gets Portuguese, the default culture.</summary>
    public static ContentCulture Portuguese { get; } = ContentCulture.Restore("pt");

    /// <summary>Gets English.</summary>
    public static ContentCulture English { get; } = ContentCulture.Restore("en");

    /// <summary>Gets the cultures: Portuguese (default) and English.</summary>
    public static SupportedCultures Cultures { get; } = SupportedCultures.Create(Portuguese, [Portuguese, English]).Value;

    /// <summary>Gets V2's price kinds: the double room is the base, Other repeats and needs a label.</summary>
    public static IReadOnlyList<PriceKindRule<TourPriceKind>> PriceKinds { get; } =
    [
        new(TourPriceKind.PerPersonDoubleRoom, IsBase: true),
        new(TourPriceKind.SingleRoomSupplement),
        new(TourPriceKind.BikeRental),
        new(TourPriceKind.EBikeRental),
        new(TourPriceKind.Other, Repeatable: true, NeedsLabel: true),
    ];

    /// <summary>Gets V2's metrics: difficulty 1–5 with a range, distance with one decimal that may be approximate, and per-day transfer and elevation.</summary>
    public static IReadOnlyList<MetricRule<TourMetric>> Metrics { get; } =
    [
        new(TourMetric.Distance, MetricScope.Offering | MetricScope.Step, 0m, 100_000m, Decimals: 1, AllowsApproximate: true),
        new(TourMetric.Transfer, MetricScope.Step, 0m, 100_000m, Decimals: 1),
        new(TourMetric.ElevationGain, MetricScope.Step, 0m, 10_000m),
        new(TourMetric.ElevationLoss, MetricScope.Step, 0m, 10_000m),
        new(TourMetric.Difficulty, MetricScope.Offering, 1m, 5m, AllowsRange: true),
    ];

    /// <summary>Gets V2's rules: one step per day, three to ten highlights, no "Dia N" titles, short limits for the tests.</summary>
    public static TourRules Rules { get; } = new(Cultures, PriceKinds, Metrics)
    {
        StepCount = OfferingStepCount.MatchesDuration,
        HighlightsCount = new CountRange(3, 10),
        ForbiddenStepTitlePrefixes = ["Dia", "Day"],
        MaxTitleLength = 40,
        MaxPrices = 6,
        MaxGalleryImages = 3,
        MaxSteps = 5,
        MaxDurationDays = 30,
        MaxListItems = 12,
        MaxParticipants = 40,
        MaxPrice = 50_000m,
    };

    /// <summary>Builds a Portuguese draft translation of a three-day tour.</summary>
    /// <param name="culture">The culture name.</param>
    /// <param name="status">The status.</param>
    /// <returns>The translation.</returns>
    public static OfferingTranslationDefinition Translation(string culture = "pt", PublicationStatus status = PublicationStatus.Draft) => new()
    {
        Culture = culture,
        Status = status,
        Title = culture == "en" ? "Romantic Road" : "Rota Romântica",
        Eyebrow = culture == "en" ? "Germany" : "Alemanha",
        Summary = culture == "en" ? "Castles and vineyards by bike." : "Castelos e vinhedos de bicicleta.",
        Description = culture == "en" ? "First paragraph.\n\nSecond paragraph." : "Primeiro parágrafo.\n\nSegundo parágrafo.",
        Highlights = culture == "en" ? ["Würzburg", "Rothenburg", "Füssen"] : ["Würzburg", "Rothenburg", "Füssen"],
        Included = ["Hotel", "Bike"],
        StepTexts =
        [
            new("Würzburg", "Arrival.", null),
            new("Rothenburg", "Ride.", "+ barco"),
            new("Füssen", "Departure.", null),
        ],
    };

    /// <summary>Builds a valid scheduled three-day tour, 20 to 22 September 2027, with a draft Portuguese translation.</summary>
    /// <param name="translations">The translations; a Portuguese draft when none is given.</param>
    /// <returns>The definition.</returns>
    public static TourDefinition Tour(params OfferingTranslationDefinition[] translations) => new()
    {
        SortOrder = 1,
        Schedule = new OfferingScheduleDefinition(OfferingAvailability.Scheduled, new DateOnly(2027, 9, 20), new DateOnly(2027, 9, 22), DurationDays: 3),
        SignUpLink = "https://forms.example.org/romantic-road",
        Currency = "EUR",
        Prices = [new(TourPriceKind.SingleRoomSupplement, 650m), new(TourPriceKind.PerPersonDoubleRoom, 2890m)],
        Participants = new ParticipantRange(6, 16),
        Metrics = [new(TourMetric.Difficulty, 2m, 3m), new(TourMetric.Distance, 320.5m, Approximate: true)],
        Steps = [new TourStep(null), new TourStep(null, [new(TourMetric.Distance, 45m)]), new TourStep(null)],
        Translations = translations.Length == 0 ? [Translation()] : translations,
    };

    /// <summary>Builds the schedule of a definition with other values.</summary>
    /// <param name="availability">The availability.</param>
    /// <param name="start">The first day.</param>
    /// <param name="end">The last day.</param>
    /// <param name="year">The year.</param>
    /// <param name="days">The duration.</param>
    /// <returns>The schedule.</returns>
    public static OfferingScheduleDefinition Schedule(OfferingAvailability availability, DateOnly? start = null, DateOnly? end = null, int? year = null, int? days = 3) =>
        new(availability, start, end, year, days);

    /// <summary>Reads the field paths of the error of a failed result, in order.</summary>
    /// <param name="error">The error.</param>
    /// <returns>The field paths.</returns>
    public static string[] Fields(ResultError? error) =>
        [.. (error?.ValidationErrors ?? throw new InvalidOperationException("No validation errors.")).Keys.Order(StringComparer.Ordinal)];

    /// <summary>Reads the messages of one field of the error of a failed result.</summary>
    /// <param name="error">The error.</param>
    /// <param name="field">The field path.</param>
    /// <returns>The messages.</returns>
    public static IReadOnlyList<string> Messages(ResultError? error, string field) =>
        (error?.ValidationErrors ?? throw new InvalidOperationException("No validation errors.")).TryGetValue(field, out var messages)
            ? messages
            : throw new InvalidOperationException("No error on the field " + field + "; errors are on " + string.Join(", ", Fields(error)) + ".");

    /// <summary>Creates an offering that must be valid.</summary>
    /// <param name="definition">The definition.</param>
    /// <param name="images">The images.</param>
    /// <returns>The offering.</returns>
    public static TourOffering Valid(TourDefinition definition, ImageLibrary? images = null) =>
        Offering.Create(definition, Rules, images ?? new ImageLibrary()).ShouldBeSuccess();

    /// <summary>Creates an offering that must be refused, and returns its error.</summary>
    /// <param name="definition">The definition.</param>
    /// <param name="images">The images.</param>
    /// <returns>The error.</returns>
    public static ResultError? Refused(TourDefinition definition, ImageLibrary? images = null)
    {
        var result = Offering.Create(definition, Rules, images ?? new ImageLibrary());
        result.IsFailure.ShouldBeTrue();
        return result.ErrorDetails;
    }

    /// <summary>Builds a full tour: published in Portuguese, a draft in English, with an "other" price labelled in both, conditions and an optional cover.</summary>
    /// <param name="cover">The cover.</param>
    /// <returns>The definition.</returns>
    public static TourDefinition FullTour(Guid? cover) =>
        Tour(Translation(status: PublicationStatus.Published) with { PriceLabels = ["Barco"] }, Translation("en") with { PriceLabels = ["Boat"] }) with
        {
            CoverId = cover,
            Prices = [new(TourPriceKind.Other, 90m), new(TourPriceKind.SingleRoomSupplement, 650m), new(TourPriceKind.PerPersonDoubleRoom, 2890m)],
            GroupCondition = new GroupConditionDefinition(8, 150m, 45),
            PaymentTerms = new PaymentTerms(30, 10, 60),
        };

    /// <summary>Asserts that restoring a definition is refused as structurally broken.</summary>
    /// <param name="definition">The stored definition.</param>
    public static void ShouldNotRestore(TourDefinition definition)
    {
        Action act = () => Offering.Restore(definition, Rules);
        act.ShouldThrow<ArgumentException>();
    }

    /// <summary>Creates a valid offering and returns its Portuguese public view.</summary>
    /// <param name="definition">The definition, with a published Portuguese translation.</param>
    /// <returns>The view.</returns>
    public static TourView PublishedView(TourDefinition definition) => Valid(definition).PublishedIn(Portuguese, Cultures).ShouldNotBeNull();

    /// <summary>Builds the published view of a three-day tour for a listing.</summary>
    /// <param name="title">The title.</param>
    /// <param name="availability">The availability.</param>
    /// <param name="sortOrder">The sort order.</param>
    /// <param name="start">The first day, or <see langword="null"/> when undated.</param>
    /// <param name="year">The year of an undated tour to be confirmed.</param>
    /// <returns>The view.</returns>
    public static TourView ListedView(string title, OfferingAvailability availability, int sortOrder, DateOnly? start, int? year)
    {
        var schedule = Schedule(availability, start, start?.AddDays(2), year);
        return PublishedView(Tour(Translation(status: PublicationStatus.Published) with { Title = title, Slug = "x" }) with { SortOrder = sortOrder, Schedule = schedule });
    }
}
