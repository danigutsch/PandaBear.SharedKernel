using System.Globalization;
using SharedKernel.Domain;
using SharedKernel.Results;

namespace SharedKernel.BuildingBlocks;

/// <summary>A range of calendar days from a first day to a last day, both included. A missing date (the default value) is reported as required.</summary>
public sealed class DateOnlyRange : ValueObject
{
    private DateOnlyRange(DateOnly startDate, DateOnly endDate)
    {
        StartDate = startDate;
        EndDate = endDate;
    }

    /// <summary>Gets the first day.</summary>
    public DateOnly StartDate { get; }

    /// <summary>Gets the last day.</summary>
    public DateOnly EndDate { get; }

    /// <summary>Gets the number of days, counting both the first and the last day.</summary>
    public int Days => EndDate.DayNumber - StartDate.DayNumber + 1;

    /// <summary>Creates a range after validating both dates.</summary>
    /// <param name="startDate">The first day.</param>
    /// <param name="endDate">The last day; it may equal the first day.</param>
    /// <returns>The range, or validation errors on <c>startDate</c> and <c>endDate</c>.</returns>
    public static Result<DateOnlyRange> Create(DateOnly startDate, DateOnly endDate)
    {
        var errors = new ValidationErrors();
        if (startDate == default)
        {
            errors.Add(Result.Invalid(detail: "Start date is required.", field: "startDate", message: "Start date is required."));
        }

        if (endDate == default)
        {
            errors.Add(Result.Invalid(detail: "End date is required.", field: "endDate", message: "End date is required."));
        }
        else if (startDate != default && endDate < startDate)
        {
            errors.Add(Result.Invalid(detail: "End date cannot be before start date.", field: "endDate", message: "End date cannot be before start date."));
        }

        return errors.HasErrors ? errors.ToResult<DateOnlyRange>() : Result.Ok(new DateOnlyRange(startDate, endDate));
    }

    /// <summary>
    /// Formats the range for people with the pattern its days call for: <see cref="DateRangePatterns.SingleDay"/> for one day,
    /// <see cref="DateRangePatterns.SameMonth"/> within one month of one year, <see cref="DateRangePatterns.SameYear"/> across months of one
    /// year, and <see cref="DateRangePatterns.Other"/> across years. Month names and digits come from the provider's culture data (ICU on
    /// Linux and macOS).
    /// </summary>
    /// <param name="patterns">The application's patterns, with <c>{0}</c> the first day and <c>{1}</c> the last.</param>
    /// <param name="provider">The culture, such as <see cref="CultureInfo.CurrentCulture"/>.</param>
    /// <returns>The display text, such as <c>20 a 29 de setembro de 2027</c>.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="FormatException">A pattern is not a valid composite format.</exception>
    public string ToDisplayString(DateRangePatterns patterns, IFormatProvider provider)
    {
        ArgumentNullException.ThrowIfNull(patterns);
        ArgumentNullException.ThrowIfNull(provider);

        var pattern = StartDate == EndDate ? patterns.SingleDay
            : StartDate.Year != EndDate.Year ? patterns.Other
            : StartDate.Month == EndDate.Month ? patterns.SameMonth
            : patterns.SameYear;
        return string.Format(provider, pattern, StartDate, EndDate);
    }

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return StartDate;
        yield return EndDate;
    }
}
