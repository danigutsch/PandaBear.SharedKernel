using PandaBear.BuildingBlocks;

namespace PandaBear.Offerings;

/// <summary>
/// The application's policy for its offerings: the cultures they are written in, its price kinds and metric kinds with their order, and
/// the limits. Every limit has a default; set the ones the application needs.
/// </summary>
/// <typeparam name="TPriceKind">The application's price kind enum.</typeparam>
/// <typeparam name="TMetric">The application's metric kind enum.</typeparam>
public sealed class OfferingRules<TPriceKind, TMetric>
    where TPriceKind : struct, Enum
    where TMetric : struct, Enum
{
    private readonly Dictionary<TPriceKind, int> _priceOrder = [];
    private readonly Dictionary<TMetric, int> _metricOrder = [];
    private readonly int _maxDurationDays = 365;
    private readonly int _minYear = 2000;
    private readonly int _maxYear = 2100;
    private readonly int _maxTitleLength = 200;
    private readonly int _maxLineLength = 300;
    private readonly int _maxSummaryLength = 1000;
    private readonly int _maxDateNoteLength = 100;
    private readonly int _maxSlugLength = Slug.DefaultMaxLength;
    private readonly int _maxParagraphs = 50;
    private readonly int _maxParagraphLength = 4000;
    private readonly int _maxPrices = 20;
    private readonly int _maxListItems = 60;
    private readonly int _maxSteps = 60;
    private readonly int _maxGalleryImages = 60;
    private readonly int _maxParticipants = 200;
    private readonly decimal _maxPrice = 1_000_000m;
    private readonly OfferingStepCount _stepCount;
    private readonly IReadOnlyList<string> _forbiddenStepTitlePrefixes = [];

    /// <summary>Initializes the rules.</summary>
    /// <param name="cultures">The cultures offerings may be written in; a translation in the default culture is required.</param>
    /// <param name="priceKinds">The price kinds, in display order, each once.</param>
    /// <param name="metrics">The metric kinds, in display order, each once.</param>
    /// <exception cref="ArgumentNullException">An argument, or a rule in a list, is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// A kind is not defined or appears twice, a metric's minimum is above its maximum, its decimals are outside 0 to 28, or its scope is
    /// none or not defined.
    /// </exception>
    public OfferingRules(SupportedCultures cultures, IReadOnlyList<PriceKindRule<TPriceKind>> priceKinds, IReadOnlyList<MetricRule<TMetric>> metrics)
    {
        ArgumentNullException.ThrowIfNull(cultures);
        ArgumentNullException.ThrowIfNull(priceKinds);
        ArgumentNullException.ThrowIfNull(metrics);

        foreach (var rule in priceKinds)
        {
            ArgumentNullException.ThrowIfNull(rule, nameof(priceKinds));
            if (!Enum.IsDefined(rule.Kind) || !_priceOrder.TryAdd(rule.Kind, _priceOrder.Count))
            {
                throw new ArgumentException("Each price kind must be defined and appear once.", nameof(priceKinds));
            }
        }

        foreach (var rule in metrics)
        {
            ArgumentNullException.ThrowIfNull(rule, nameof(metrics));
            if (!Enum.IsDefined(rule.Kind) || !_metricOrder.TryAdd(rule.Kind, _metricOrder.Count))
            {
                throw new ArgumentException("Each metric kind must be defined and appear once.", nameof(metrics));
            }

            if (rule.Min > rule.Max || rule.Decimals is < 0 or > 28 || rule.Scope == MetricScope.None
                || (rule.Scope & ~(MetricScope.Offering | MetricScope.Step)) != 0)
            {
                throw new ArgumentException("A metric needs a minimum not above its maximum, 0 to 28 decimals and a scope.", nameof(metrics));
            }
        }

        Cultures = cultures;
        PriceKinds = [.. priceKinds];
        Metrics = [.. metrics];
    }

    /// <summary>Gets the cultures offerings may be written in.</summary>
    public SupportedCultures Cultures { get; }

    /// <summary>Gets the price kinds in display order.</summary>
    public IReadOnlyList<PriceKindRule<TPriceKind>> PriceKinds { get; }

    /// <summary>Gets the metric kinds in display order.</summary>
    public IReadOnlyList<MetricRule<TMetric>> Metrics { get; }

    /// <summary>Gets the longest duration in days. The default is 365.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is zero or negative.</exception>
    public int MaxDurationDays { get => _maxDurationDays; init => _maxDurationDays = Positive(value); }

    /// <summary>Gets the earliest year of an offering. The default is 2000.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is zero or negative.</exception>
    public int MinYear { get => _minYear; init => _minYear = Positive(value); }

    /// <summary>Gets the latest year of an offering. The default is 2100.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is zero or negative.</exception>
    public int MaxYear { get => _maxYear; init => _maxYear = Positive(value); }

    /// <summary>Gets the longest title, SEO title, price label and step title. The default is 200.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is zero or negative.</exception>
    public int MaxTitleLength { get => _maxTitleLength; init => _maxTitleLength = Positive(value); }

    /// <summary>Gets the longest one-line text: the subtitle, the eyebrow, a list item and a step note. The default is 300.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is zero or negative.</exception>
    public int MaxLineLength { get => _maxLineLength; init => _maxLineLength = Positive(value); }

    /// <summary>Gets the longest summary and SEO description. The default is 1000.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is zero or negative.</exception>
    public int MaxSummaryLength { get => _maxSummaryLength; init => _maxSummaryLength = Positive(value); }

    /// <summary>Gets the longest date note. The default is 100.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is zero or negative.</exception>
    public int MaxDateNoteLength { get => _maxDateNoteLength; init => _maxDateNoteLength = Positive(value); }

    /// <summary>Gets the longest slug. The default is <see cref="Slug.DefaultMaxLength"/>.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is zero or negative.</exception>
    public int MaxSlugLength { get => _maxSlugLength; init => _maxSlugLength = Positive(value); }

    /// <summary>Gets the most paragraphs of a description or a step text. The default is 50.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is zero or negative.</exception>
    public int MaxParagraphs { get => _maxParagraphs; init => _maxParagraphs = Positive(value); }

    /// <summary>Gets the longest paragraph. The default is 4000.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is zero or negative.</exception>
    public int MaxParagraphLength { get => _maxParagraphLength; init => _maxParagraphLength = Positive(value); }

    /// <summary>Gets the most prices. The default is 20.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is zero or negative.</exception>
    public int MaxPrices { get => _maxPrices; init => _maxPrices = Positive(value); }

    /// <summary>Gets the most items of a feature list (highlights, included, not included, practical information). The default is 60.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is zero or negative.</exception>
    public int MaxListItems { get => _maxListItems; init => _maxListItems = Positive(value); }

    /// <summary>Gets the most steps. The default is 60.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is zero or negative.</exception>
    public int MaxSteps { get => _maxSteps; init => _maxSteps = Positive(value); }

    /// <summary>Gets the most gallery images. The default is 60.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is zero or negative.</exception>
    public int MaxGalleryImages { get => _maxGalleryImages; init => _maxGalleryImages = Positive(value); }

    /// <summary>Gets the largest group size, the bound of <see cref="ParticipantRange"/> and of a group condition without a largest group. The default is 200.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is zero or negative.</exception>
    public int MaxParticipants { get => _maxParticipants; init => _maxParticipants = Positive(value); }

    /// <summary>Gets the largest price or surcharge. The default is 1,000,000.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is zero or negative.</exception>
    public decimal MaxPrice
    {
        get => _maxPrice;
        init
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
            _maxPrice = value;
        }
    }

    /// <summary>Gets how many steps an offering may have. The default is <see cref="OfferingStepCount.Free"/>.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not defined.</exception>
    public OfferingStepCount StepCount
    {
        get => _stepCount;
        init => _stepCount = Enum.IsDefined(value) ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The step count rule is not defined.");
    }

    /// <summary>Gets how many highlights a translation may have when it has any; <see langword="null"/>, the default, sets no range.</summary>
    public CountRange? HighlightsCount { get; init; }

    /// <summary>
    /// Gets the words a step title must not begin with when a number follows, such as <c>Dia</c> and <c>Day</c>, because the number comes
    /// from the step's position. Case is ignored. The default is none.
    /// </summary>
    /// <exception cref="ArgumentNullException">The value, or a prefix in it, is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A prefix is empty or blank.</exception>
    public IReadOnlyList<string> ForbiddenStepTitlePrefixes
    {
        get => _forbiddenStepTitlePrefixes;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            foreach (var prefix in value)
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(prefix, nameof(value));
            }

            _forbiddenStepTitlePrefixes = [.. value];
        }
    }

    /// <summary>Finds the rule of a price kind.</summary>
    /// <param name="kind">The kind.</param>
    /// <param name="order">Its position in the display order.</param>
    /// <returns>The rule, or <see langword="null"/> when the kind has none.</returns>
    internal PriceKindRule<TPriceKind>? PriceKindOf(TPriceKind kind, out int order) =>
        _priceOrder.TryGetValue(kind, out order) ? PriceKinds[order] : null;

    /// <summary>Finds the rule of a metric kind.</summary>
    /// <param name="kind">The kind.</param>
    /// <param name="order">Its position in the display order.</param>
    /// <returns>The rule, or <see langword="null"/> when the kind has none.</returns>
    internal MetricRule<TMetric>? MetricOf(TMetric kind, out int order) =>
        _metricOrder.TryGetValue(kind, out order) ? Metrics[order] : null;

    private static int Positive(int value)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
        return value;
    }
}
