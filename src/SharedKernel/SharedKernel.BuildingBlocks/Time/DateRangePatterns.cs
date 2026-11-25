namespace SharedKernel.BuildingBlocks;

/// <summary>
/// The composite formats a <see cref="DateOnlyRange"/> is displayed with, chosen by what its first and last day share. In each, <c>{0}</c>
/// is the first day and <c>{1}</c> the last, both <see cref="DateOnly"/> values that take custom date formats, for example
/// <c>{0:%d} a {1:d 'de' MMMM 'de' yyyy}</c>, which gives <c>20 a 29 de setembro de 2027</c> in pt-BR. The words are the application's.
/// </summary>
public sealed class DateRangePatterns
{
    /// <summary>Initializes the patterns.</summary>
    /// <param name="singleDay">The pattern of a one-day range.</param>
    /// <param name="sameMonth">The pattern of a range within one month of one year.</param>
    /// <param name="sameYear">The pattern of a range across months of one year.</param>
    /// <param name="other">The pattern of a range across years.</param>
    /// <exception cref="ArgumentException">A pattern is <see langword="null"/>, empty or blank.</exception>
    public DateRangePatterns(string singleDay, string sameMonth, string sameYear, string other)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(singleDay);
        ArgumentException.ThrowIfNullOrWhiteSpace(sameMonth);
        ArgumentException.ThrowIfNullOrWhiteSpace(sameYear);
        ArgumentException.ThrowIfNullOrWhiteSpace(other);

        SingleDay = singleDay;
        SameMonth = sameMonth;
        SameYear = sameYear;
        Other = other;
    }

    /// <summary>Gets the pattern of a one-day range.</summary>
    public string SingleDay { get; }

    /// <summary>Gets the pattern of a range within one month of one year.</summary>
    public string SameMonth { get; }

    /// <summary>Gets the pattern of a range across months of one year.</summary>
    public string SameYear { get; }

    /// <summary>Gets the pattern of a range across years.</summary>
    public string Other { get; }
}
