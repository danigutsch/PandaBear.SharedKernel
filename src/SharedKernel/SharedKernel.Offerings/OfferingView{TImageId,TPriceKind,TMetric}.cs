using System.Collections.ObjectModel;
using SharedKernel.BuildingBlocks;

namespace SharedKernel.Offerings;

/// <summary>
/// The public view of a published offering in one culture, built by <see cref="Offering{TImageId, TPriceKind, TMetric}.PublishedIn"/>: the
/// text of one whole translation and the offering's language-neutral data.
/// </summary>
/// <typeparam name="TImageId">The image id type.</typeparam>
/// <typeparam name="TPriceKind">The application's price kind enum.</typeparam>
/// <typeparam name="TMetric">The application's metric kind enum.</typeparam>
public sealed class OfferingView<TImageId, TPriceKind, TMetric>
    where TImageId : struct, IEquatable<TImageId>
    where TPriceKind : struct, Enum
    where TMetric : struct, Enum
{
    private readonly OfferingData<TImageId, TPriceKind, TMetric> _data;
    private readonly OfferingTranslation _translation;

    /// <summary>Initializes the view.</summary>
    /// <param name="data">The offering's data.</param>
    /// <param name="translation">The resolved published translation.</param>
    internal OfferingView(OfferingData<TImageId, TPriceKind, TMetric> data, OfferingTranslation translation)
    {
        _data = data;
        _translation = translation;
        Prices = PricesOf(data, translation);
        Steps = [.. data.Steps.Zip(translation.StepTexts, (step, text) => (step, text))
            .Select((pair, index) => new ProgramStepView<TImageId, TMetric>(index + 1, pair.step, pair.text))];
        CallToAction = OfferingCallToActions.For(data.Schedule.Availability, data.SignUpLink is not null);
    }

    /// <summary>Gets the culture of the text, which differs from the requested one when the view fell back; mark the text with its <c>lang</c>.</summary>
    public ContentCulture Culture => _translation.Culture;

    /// <summary>Gets the slug of the offering's address.</summary>
    public Slug Slug => _translation.Slug;

    /// <summary>Gets the title.</summary>
    public string Title => _translation.Title;

    /// <summary>Gets the optional tagline.</summary>
    public string? Subtitle => _translation.Subtitle;

    /// <summary>Gets the optional short line above the title.</summary>
    public string? Eyebrow => _translation.Eyebrow;

    /// <summary>Gets the summary.</summary>
    public string? Summary => _translation.Summary;

    /// <summary>Gets the description's paragraphs.</summary>
    public IReadOnlyList<string> Description => _translation.Description;

    /// <summary>Gets the date note of an undated offering.</summary>
    public string? DateNote => _translation.DateNote;

    /// <summary>Gets the search-engine title: the SEO title, else the title.</summary>
    public string SeoTitle => _translation.SeoTitle ?? _translation.Title;

    /// <summary>Gets the search-engine description: the SEO description, else the summary.</summary>
    public string? SeoDescription => _translation.SeoDescription ?? _translation.Summary;

    /// <summary>Gets the highlights.</summary>
    public IReadOnlyList<string> Highlights => _translation.Highlights;

    /// <summary>Gets what is included.</summary>
    public IReadOnlyList<string> Included => _translation.Included;

    /// <summary>Gets what is not included.</summary>
    public IReadOnlyList<string> NotIncluded => _translation.NotIncluded;

    /// <summary>Gets the practical information.</summary>
    public IReadOnlyList<string> PracticalInfo => _translation.PracticalInfo;

    /// <summary>Gets the sort order.</summary>
    public int SortOrder => _data.SortOrder;

    /// <summary>Gets the schedule.</summary>
    public OfferingSchedule Schedule => _data.Schedule;

    /// <summary>Gets the availability.</summary>
    public OfferingAvailability Availability => _data.Schedule.Availability;

    /// <summary>Gets the dates, or <see langword="null"/> when undated.</summary>
    public DateOnlyRange? Dates => _data.Schedule.Dates;

    /// <summary>Gets the year of a dated or to-be-confirmed offering.</summary>
    public int? Year => _data.Schedule.Year;

    /// <summary>Gets the duration in days.</summary>
    public int DurationDays => _data.Schedule.DurationDays;

    /// <summary>Gets the number of nights.</summary>
    public int Nights => _data.Schedule.Nights;

    /// <summary>
    /// Gets what the page asks the reader to do: <see cref="OfferingCallToAction.SignUp"/> for a scheduled offering with a sign-up link,
    /// else <see cref="OfferingCallToAction.Contact"/>.
    /// </summary>
    public OfferingCallToAction CallToAction { get; }

    /// <summary>Gets the sign-up link, only when <see cref="CallToAction"/> is <see cref="OfferingCallToAction.SignUp"/>.</summary>
    public WebUrl? SignUpLink => CallToAction == OfferingCallToAction.SignUp ? _data.SignUpLink : null;

    /// <summary>Gets the currency.</summary>
    public Currency? Currency => _data.Currency;

    /// <summary>Gets the prices, in display order, with the labels of this culture.</summary>
    public IReadOnlyList<PriceView<TPriceKind>> Prices { get; }

    /// <summary>Gets the from price.</summary>
    public Money? FromPrice => _data.FromPrice;

    /// <summary>Gets the group sizes.</summary>
    public ParticipantRange? Participants => _data.Participants;

    /// <summary>Gets the minimum-group condition.</summary>
    public GroupCondition? GroupCondition => _data.GroupCondition;

    /// <summary>Gets the payment terms.</summary>
    public PaymentTerms? PaymentTerms => _data.PaymentTerms;

    /// <summary>Gets the offering's metrics.</summary>
    public IReadOnlyList<Metric<TMetric>> Metrics => _data.Metrics;

    /// <summary>Gets the steps with their text in this culture.</summary>
    public IReadOnlyList<ProgramStepView<TImageId, TMetric>> Steps { get; }

    /// <summary>Gets the cover image.</summary>
    public TImageId? CoverId => _data.CoverId;

    /// <summary>Gets the gallery images.</summary>
    public IReadOnlyList<TImageId> GalleryIds => _data.GalleryIds;

    /// <summary>Gets every image the view shows.</summary>
    public IReadOnlyList<TImageId> ImageIds => _data.ImageIds;

    private static ReadOnlyCollection<PriceView<TPriceKind>> PricesOf(OfferingData<TImageId, TPriceKind, TMetric> data, OfferingTranslation translation)
    {
        var prices = new List<PriceView<TPriceKind>>(data.Prices.Count);
        var label = 0;
        foreach (var line in data.Prices)
        {
            var rule = data.KindOf(line.Kind);
            string? text = null;
            if (rule?.NeedsLabel == true)
            {
                text = label < translation.PriceLabels.Count ? translation.PriceLabels[label] : null;
                label++;
            }

            prices.Add(new PriceView<TPriceKind>(line.Kind, line.Amount, text, rule?.IsBase == true));
        }

        return prices.AsReadOnly();
    }
}
