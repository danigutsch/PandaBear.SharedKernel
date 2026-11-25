using System.Globalization;
using SharedKernel.BuildingBlocks;

namespace SharedKernel.Offerings;

/// <summary>
/// The field paths of offering validation errors, so an application can place each error on its control. Metric paths use the kind's
/// name in camelCase (<c>metrics.distance</c>). The fields of a translation are prefixed with its culture, for example
/// <c>pt-BR.title</c>; see <see cref="Of"/>.
/// </summary>
public static class OfferingFields
{
    /// <summary>The sort order.</summary>
    public const string SortOrder = "sortOrder";

    /// <summary>The availability: missing or not supported.</summary>
    public const string Availability = "availability";

    /// <summary>The first day, and the dates as a whole: required, refused, or outside the years.</summary>
    public const string StartDate = "startDate";

    /// <summary>The last day: required with a first day, or before it.</summary>
    public const string EndDate = "endDate";

    /// <summary>The year of an undated offering whose dates are to be confirmed.</summary>
    public const string Year = "year";

    /// <summary>The duration in days.</summary>
    public const string DurationDays = "durationDays";

    /// <summary>The sign-up link.</summary>
    public const string SignUpLink = "signUpLink";

    /// <summary>The currency.</summary>
    public const string Currency = "currency";

    /// <summary>The list of prices: too many.</summary>
    public const string Prices = "prices";

    /// <summary>The smallest group.</summary>
    public const string ParticipantsMin = "participants.min";

    /// <summary>The largest group.</summary>
    public const string ParticipantsMax = "participants.max";

    /// <summary>The group condition's minimum participants.</summary>
    public const string GroupConditionMinimumParticipants = "groupCondition.minimumParticipants";

    /// <summary>The group condition's surcharge per missing participant.</summary>
    public const string GroupConditionSurchargePerMissing = "groupCondition.surchargePerMissing";

    /// <summary>The group condition's due days.</summary>
    public const string GroupConditionDueDays = "groupCondition.dueDays";

    /// <summary>The deposit percentage.</summary>
    public const string PaymentTermsDepositPercent = "paymentTerms.depositPercent";

    /// <summary>The most instalments.</summary>
    public const string PaymentTermsInstalments = "paymentTerms.instalments";

    /// <summary>The days before the start the balance is due.</summary>
    public const string PaymentTermsBalanceDueDays = "paymentTerms.balanceDueDays";

    /// <summary>The offering's metrics as a list: a kind that is not supported.</summary>
    public const string Metrics = "metrics";

    /// <summary>The steps: too many, or not one per day; in a translation, too many or missing step texts.</summary>
    public const string Steps = "steps";

    /// <summary>The cover image.</summary>
    public const string CoverId = "coverId";

    /// <summary>The gallery images.</summary>
    public const string GalleryIds = "galleryIds";

    /// <summary>The set of translations: an unknown culture, a culture given twice, or no translation in the default culture.</summary>
    public const string Translations = "translations";

    /// <summary>The title of a translation.</summary>
    public const string Title = "title";

    /// <summary>The slug of a translation.</summary>
    public const string Slug = "slug";

    /// <summary>The publication status of a translation.</summary>
    public const string Status = "status";

    /// <summary>The subtitle of a translation.</summary>
    public const string Subtitle = "subtitle";

    /// <summary>The eyebrow of a translation.</summary>
    public const string Eyebrow = "eyebrow";

    /// <summary>The summary of a translation.</summary>
    public const string Summary = "summary";

    /// <summary>The description of a translation.</summary>
    public const string Description = "description";

    /// <summary>The date note of a translation.</summary>
    public const string DateNote = "dateNote";

    /// <summary>The SEO title of a translation.</summary>
    public const string SeoTitle = "seoTitle";

    /// <summary>The SEO description of a translation.</summary>
    public const string SeoDescription = "seoDescription";

    /// <summary>The highlights of a translation.</summary>
    public const string Highlights = "highlights";

    /// <summary>What a translation lists as included.</summary>
    public const string Included = "included";

    /// <summary>What a translation lists as not included.</summary>
    public const string NotIncluded = "notIncluded";

    /// <summary>The practical information of a translation.</summary>
    public const string PracticalInfo = "practicalInfo";

    /// <summary>The price labels of a translation.</summary>
    public const string PriceLabels = "priceLabels";

    /// <summary>The publish rule of a translation's images: images that are not eligible in its culture.</summary>
    public const string Images = "images";

    /// <summary>Builds the path of a price's kind, such as <c>prices[0].kind</c>.</summary>
    /// <param name="index">The position of the price as entered.</param>
    /// <returns>The path.</returns>
    public static string PriceKind(int index) => Indexed(Prices, index, "kind");

    /// <summary>Builds the path of a price's amount, such as <c>prices[0].amount</c>.</summary>
    /// <param name="index">The position of the price as entered.</param>
    /// <returns>The path.</returns>
    public static string PriceAmount(int index) => Indexed(Prices, index, "amount");

    /// <summary>Builds the path of an offering metric, such as <c>metrics.distance</c>.</summary>
    /// <typeparam name="TMetric">The application's metric kind enum.</typeparam>
    /// <param name="kind">The kind.</param>
    /// <returns>The path.</returns>
    public static string Metric<TMetric>(TMetric kind)
        where TMetric : struct, Enum => Metrics + "." + EnumNames.CamelCase(kind);

    /// <summary>Builds the path of an offering metric's upper value, such as <c>metrics.difficulty.upper</c>.</summary>
    /// <typeparam name="TMetric">The application's metric kind enum.</typeparam>
    /// <param name="kind">The kind.</param>
    /// <returns>The path.</returns>
    public static string MetricUpper<TMetric>(TMetric kind)
        where TMetric : struct, Enum => Metric(kind) + ".upper";

    /// <summary>Builds the path of a step's image, such as <c>steps[0].imageId</c>.</summary>
    /// <param name="index">The position of the step.</param>
    /// <returns>The path.</returns>
    public static string StepImage(int index) => Indexed(Steps, index, "imageId");

    /// <summary>Builds the path of a step's metric list, such as <c>steps[0].metrics</c>.</summary>
    /// <param name="index">The position of the step.</param>
    /// <returns>The path.</returns>
    public static string StepMetrics(int index) => Indexed(Steps, index, Metrics);

    /// <summary>Builds the path of a step metric, such as <c>steps[0].metrics.distance</c>.</summary>
    /// <typeparam name="TMetric">The application's metric kind enum.</typeparam>
    /// <param name="index">The position of the step.</param>
    /// <param name="kind">The kind.</param>
    /// <returns>The path.</returns>
    public static string StepMetric<TMetric>(int index, TMetric kind)
        where TMetric : struct, Enum => StepMetrics(index) + "." + EnumNames.CamelCase(kind);

    /// <summary>Builds the path of a step metric's upper value, such as <c>steps[0].metrics.distance.upper</c>.</summary>
    /// <typeparam name="TMetric">The application's metric kind enum.</typeparam>
    /// <param name="index">The position of the step.</param>
    /// <param name="kind">The kind.</param>
    /// <returns>The path.</returns>
    public static string StepMetricUpper<TMetric>(int index, TMetric kind)
        where TMetric : struct, Enum => StepMetric(index, kind) + ".upper";

    /// <summary>Builds the field of a step's title within a translation, such as <c>steps[0].title</c>; prefix it with <see cref="Of"/>.</summary>
    /// <param name="index">The position of the step.</param>
    /// <returns>The field.</returns>
    public static string StepTitle(int index) => Indexed(Steps, index, "title");

    /// <summary>Builds the field of a step's text within a translation, such as <c>steps[0].text</c>; prefix it with <see cref="Of"/>.</summary>
    /// <param name="index">The position of the step.</param>
    /// <returns>The field.</returns>
    public static string StepText(int index) => Indexed(Steps, index, "text");

    /// <summary>Builds the field of a step's note within a translation, such as <c>steps[0].note</c>; prefix it with <see cref="Of"/>.</summary>
    /// <param name="index">The position of the step.</param>
    /// <returns>The field.</returns>
    public static string StepNote(int index) => Indexed(Steps, index, "note");

    /// <summary>Builds the path of a translation's field, such as <c>pt-BR.title</c> or <c>en.steps[2].note</c>.</summary>
    /// <param name="culture">The culture of the translation.</param>
    /// <param name="field">The field, such as <see cref="Title"/> or <see cref="StepNote"/>.</param>
    /// <returns>The path.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static string Of(ContentCulture culture, string field)
    {
        ArgumentNullException.ThrowIfNull(culture);
        ArgumentNullException.ThrowIfNull(field);

        return culture.Value + "." + field;
    }

    private static string Indexed(string list, int index, string field) =>
        string.Create(CultureInfo.InvariantCulture, $"{list}[{index}].{field}");
}
