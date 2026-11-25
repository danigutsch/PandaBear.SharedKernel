using SharedKernel.Results;

namespace SharedKernel.BuildingBlocks;

/// <summary>
/// Represents a validated date range.
/// </summary>
public sealed class DateRange : ValueObject
{
    private DateRange(DateTime startDate, DateTime endDate)
    {
        StartDate = startDate;
        EndDate = endDate;
    }

    /// <summary>
    /// Gets the start date of the range.
    /// </summary>
    public DateTime StartDate { get; }

    /// <summary>
    /// Gets the end date of the range.
    /// </summary>
    public DateTime EndDate { get; }

    /// <summary>
    /// Gets the duration of the date range in days.
    /// </summary>
    public double DurationDays => (EndDate - StartDate).TotalDays;

    /// <summary>
    /// Creates a new date range with validation.
    /// </summary>
    /// <param name="startDate">The start date of the range.</param>
    /// <param name="endDate">The end date of the range.</param>
    /// <returns>A valid date range or validation errors.</returns>
    public static Result<DateRange> Create(DateTime startDate, DateTime endDate)
    {
        if (endDate <= startDate)
        {
            return Result.Invalid<DateRange>(
                detail: "End date must be after start date.",
                field: "schedule",
                message: "End date must be after start date.");
        }

        return Result.Ok(new DateRange(startDate, endDate));
    }

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return StartDate;
        yield return EndDate;
    }
}
