using SharedKernel.BuildingBlocks;

namespace SharedKernel.Offerings;

/// <summary>
/// The display sentences of an offering, with the application's formats: every word is the application's copy, and the numbers, dates and
/// money follow the given culture. Plurals have two forms, one and other (see <see cref="PluralFormat"/>). For a public view, pass the
/// culture of <see cref="OfferingView{TImageId, TPriceKind, TMetric}.Culture"/> with the formats of that language, so the sentences match
/// the text shown when the view fell back to another language.
/// </summary>
public static class OfferingText
{
    /// <summary>
    /// Builds the date line shared by the card and the page: the dates with <paramref name="patterns"/>; else the date note; else, for an
    /// undated offering whose dates are to be confirmed, <paramref name="toBeConfirmedFormat"/> with the year as <c>{0}</c>; else
    /// <see langword="null"/> (an offering on request, whose badge says it).
    /// </summary>
    /// <param name="schedule">The schedule.</param>
    /// <param name="dateNote">The date note of the translation shown.</param>
    /// <param name="patterns">The date range patterns.</param>
    /// <param name="toBeConfirmedFormat">The format of a year to be confirmed, such as <c>Datas a confirmar, {0}</c>.</param>
    /// <param name="provider">The culture.</param>
    /// <returns>The line, or <see langword="null"/>.</returns>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The schedule's availability is <see cref="OfferingAvailability.Unspecified"/> or not defined.</exception>
    public static string? DateLine(OfferingSchedule schedule, string? dateNote, DateRangePatterns patterns, string toBeConfirmedFormat, IFormatProvider provider)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        ArgumentNullException.ThrowIfNull(patterns);
        ArgumentNullException.ThrowIfNull(toBeConfirmedFormat);
        ArgumentNullException.ThrowIfNull(provider);
        OfferingSchedules.Check(schedule, nameof(schedule));

        if (schedule.Dates is { } dates)
        {
            return dates.ToDisplayString(patterns, provider);
        }

        if (!string.IsNullOrWhiteSpace(dateNote))
        {
            return dateNote;
        }

        return schedule.Availability == OfferingAvailability.DatesToBeConfirmed && schedule.Year is { } year
            ? string.Format(provider, toBeConfirmedFormat, year)
            : null;
    }

    /// <summary>Builds the date line of a public view; see <see cref="DateLine(OfferingSchedule, string?, DateRangePatterns, string, IFormatProvider)"/>.</summary>
    /// <typeparam name="TImageId">The image id type.</typeparam>
    /// <typeparam name="TPriceKind">The application's price kind enum.</typeparam>
    /// <typeparam name="TMetric">The application's metric kind enum.</typeparam>
    /// <param name="view">The view.</param>
    /// <param name="patterns">The date range patterns.</param>
    /// <param name="toBeConfirmedFormat">The format of a year to be confirmed.</param>
    /// <param name="provider">The culture.</param>
    /// <returns>The line, or <see langword="null"/>.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static string? DateLine<TImageId, TPriceKind, TMetric>(
        OfferingView<TImageId, TPriceKind, TMetric> view,
        DateRangePatterns patterns,
        string toBeConfirmedFormat,
        IFormatProvider provider)
        where TImageId : struct, IEquatable<TImageId>
        where TPriceKind : struct, Enum
        where TMetric : struct, Enum
    {
        ArgumentNullException.ThrowIfNull(view);
        return DateLine(view.Schedule, view.DateNote, patterns, toBeConfirmedFormat, provider);
    }

    /// <summary>Builds the duration, such as <c>["8 dias", "7 noites"]</c>; a one-day offering has no nights part.</summary>
    /// <param name="schedule">The schedule.</param>
    /// <param name="days">The format of the days.</param>
    /// <param name="nights">The format of the nights.</param>
    /// <param name="provider">The culture.</param>
    /// <returns>The parts.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The schedule's availability is <see cref="OfferingAvailability.Unspecified"/> or not defined.</exception>
    public static IReadOnlyList<string> Duration(OfferingSchedule schedule, PluralFormat days, PluralFormat nights, IFormatProvider provider)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        ArgumentNullException.ThrowIfNull(days);
        ArgumentNullException.ThrowIfNull(nights);
        ArgumentNullException.ThrowIfNull(provider);
        OfferingSchedules.Check(schedule, nameof(schedule));

        return schedule.Nights > 0
            ? [days.Format(schedule.DurationDays, provider), nights.Format(schedule.Nights, provider)]
            : [days.Format(schedule.DurationDays, provider)];
    }

    /// <summary>Builds the group size: a range, a smallest group alone, a largest group alone, or one fixed size.</summary>
    /// <param name="range">The group sizes.</param>
    /// <param name="formats">The formats.</param>
    /// <param name="provider">The culture.</param>
    /// <returns>The text, or <see langword="null"/> without a group size.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="formats"/> or <paramref name="provider"/> is <see langword="null"/>.</exception>
    public static string? Participants(ParticipantRange? range, ParticipantFormats formats, IFormatProvider provider)
    {
        ArgumentNullException.ThrowIfNull(formats);
        ArgumentNullException.ThrowIfNull(provider);

        return range switch
        {
            { Min: { } min, Max: { } max } when min == max => formats.Exact.Format(min, provider),
            { Min: { } min, Max: { } max } => string.Format(provider, formats.Range, min, max),
            { Min: { } min } => formats.Minimum.Format(min, provider),
            { Max: { } max } => formats.Maximum.Format(max, provider),
            _ => null,
        };
    }

    /// <summary>
    /// Builds the group condition sentence from the application's formats: the minimum participants and the due days in their one and
    /// other forms (<c>1 dia</c>, <c>45 dias</c>), and the surcharge per missing participant as money for people.
    /// </summary>
    /// <param name="condition">The condition.</param>
    /// <param name="formats">The application's sentence and forms.</param>
    /// <param name="money">How the surcharge is displayed.</param>
    /// <param name="provider">The culture.</param>
    /// <returns>The sentence.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static string GroupCondition(GroupCondition condition, GroupConditionFormats formats, MoneyDisplayOptions money, IFormatProvider provider)
    {
        ArgumentNullException.ThrowIfNull(condition);
        ArgumentNullException.ThrowIfNull(formats);
        ArgumentNullException.ThrowIfNull(money);
        ArgumentNullException.ThrowIfNull(provider);

        return string.Format(
            provider,
            formats.Sentence,
            formats.Minimum.Format(condition.MinimumParticipants, provider),
            condition.SurchargePerMissing.ToDisplayString(provider, money),
            formats.DueDays.Format(condition.DueDays, provider));
    }

    /// <summary>Builds one sentence per given payment term, in the order deposit, instalments, balance.</summary>
    /// <param name="terms">The terms.</param>
    /// <param name="formats">The formats.</param>
    /// <param name="provider">The culture.</param>
    /// <returns>The sentences.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static IReadOnlyList<string> PaymentTerms(PaymentTerms terms, PaymentTermsFormats formats, IFormatProvider provider)
    {
        ArgumentNullException.ThrowIfNull(terms);
        ArgumentNullException.ThrowIfNull(formats);
        ArgumentNullException.ThrowIfNull(provider);

        var sentences = new List<string>(3);
        if (terms.DepositPercent is { } deposit)
        {
            sentences.Add(string.Format(provider, formats.Deposit, deposit));
        }

        if (terms.Instalments is { } instalments)
        {
            sentences.Add(formats.Instalments.Format(instalments, provider));
        }

        if (terms.BalanceDueDays is { } balance)
        {
            sentences.Add(formats.BalanceDue.Format(balance, provider));
        }

        return sentences.AsReadOnly();
    }

    /// <summary>
    /// Builds a metric: the value, or a range joined with an en dash (<c>2–3</c>), with at most the format's decimals and the culture's
    /// separators, in the format's pattern, marked approximate when it is.
    /// </summary>
    /// <typeparam name="TMetric">The application's metric kind enum.</typeparam>
    /// <param name="metric">The metric.</param>
    /// <param name="format">The application's format of its kind.</param>
    /// <param name="provider">The culture.</param>
    /// <returns>The text, such as <c>aprox. 320,5 km</c>.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static string Metric<TMetric>(Metric<TMetric> metric, MetricFormat format, IFormatProvider provider)
        where TMetric : struct, Enum
    {
        ArgumentNullException.ThrowIfNull(metric);
        ArgumentNullException.ThrowIfNull(format);
        ArgumentNullException.ThrowIfNull(provider);

        var number = format.Decimals == 0 ? "#,0" : "#,0." + new string('#', format.Decimals);
        var value = metric.Value.ToString(number, provider);
        if (metric.Upper is { } upper)
        {
            value += "–" + upper.ToString(number, provider);
        }

        var text = string.Format(provider, format.Pattern, value);
        return metric.Approximate ? string.Format(provider, format.ApproximatePattern, text) : text;
    }
}
