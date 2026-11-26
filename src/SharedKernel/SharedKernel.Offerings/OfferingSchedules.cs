using SharedKernel.BuildingBlocks;
using SharedKernel.Results;

namespace SharedKernel.Offerings;

/// <summary>Validates an offering schedule independently of the offering aggregate.</summary>
public static class OfferingSchedules
{
    /// <summary>
    /// Validates entered schedule data. A dated offering takes its year from its first day. Its explicit duration must equal the
    /// number of days in its range, counting both ends.
    /// </summary>
    /// <param name="definition">The entered schedule.</param>
    /// <param name="minYear">The earliest allowed year, inclusive.</param>
    /// <param name="maxYear">The latest allowed year, inclusive.</param>
    /// <param name="maxDurationDays">The greatest allowed duration in days, inclusive.</param>
    /// <returns>The validated schedule, or validation errors on <see cref="OfferingFields"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="definition"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="minYear"/> or <paramref name="maxDurationDays"/> is below one, or <paramref name="maxYear"/> is below
    /// <paramref name="minYear"/>.
    /// </exception>
    public static Result<OfferingSchedule> Create(OfferingScheduleDefinition definition, int minYear, int maxYear, int maxDurationDays)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(minYear);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxYear, minYear);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxDurationDays);

        var errors = new ValidationErrors();
        var schedule = Validate(definition, minYear, maxYear, maxDurationDays, errors);
        return schedule is null ? errors.ToResult<OfferingSchedule>() : Result.Ok(schedule);
    }

    /// <summary>Validates a schedule and records errors in the aggregate's validation flow.</summary>
    /// <param name="definition">The entered schedule.</param>
    /// <param name="minYear">The earliest allowed year, inclusive.</param>
    /// <param name="maxYear">The latest allowed year, inclusive.</param>
    /// <param name="maxDurationDays">The greatest allowed duration in days, inclusive.</param>
    /// <param name="errors">The collector for field errors.</param>
    /// <returns>The schedule, or <see langword="null"/> when validation fails.</returns>
    internal static OfferingSchedule? Validate(OfferingScheduleDefinition definition, int minYear, int maxYear, int maxDurationDays, ValidationErrors errors)
    {
        var availability = definition.Availability;
        var defined = IsValid(availability);
        if (!defined)
        {
            errors.Add(OfferingFields.Availability, availability == OfferingAvailability.Unspecified ? "Availability is required." : "The availability is not supported.");
        }

        var dated = definition.StartDate is not null || definition.EndDate is not null;
        var dates = Dates(definition, minYear, maxYear, errors);
        if (defined)
        {
            AvailabilityRules(availability, dated, definition.Year, errors);
        }

        if (dated && definition.Year is not null)
        {
            errors.Add(OfferingFields.Year, "The year comes from the dates; leave it empty.");
        }
        else if (!dated && availability != OfferingAvailability.OnRequest && definition.Year is { } year && !InYears(year, minYear, maxYear))
        {
            errors.Add(OfferingFields.Year, FormattableString.Invariant($"Year must be between {minYear} and {maxYear}."));
        }

        Duration(definition.DurationDays, dates, maxDurationDays, errors);

        return !errors.HasErrors && definition.DurationDays is { } days
            ? new OfferingSchedule(availability, dates, dates?.StartDate.Year ?? definition.Year, days)
            : null;
    }

    /// <summary>Determines whether an availability is a valid state of an offering: defined and not <see cref="OfferingAvailability.Unspecified"/>.</summary>
    /// <param name="availability">The availability.</param>
    /// <returns><see langword="true"/> when it is valid.</returns>
    internal static bool IsValid(OfferingAvailability availability) =>
        availability != OfferingAvailability.Unspecified && Enum.IsDefined(availability);

    /// <summary>Refuses a schedule whose availability is not a valid state of an offering.</summary>
    /// <param name="schedule">The schedule.</param>
    /// <param name="name">The parameter name.</param>
    /// <exception cref="ArgumentException">The availability is <see cref="OfferingAvailability.Unspecified"/> or not defined.</exception>
    internal static void Check(OfferingSchedule schedule, string name)
    {
        if (!IsValid(schedule.Availability))
        {
            throw new ArgumentException("The schedule's availability is not set or not defined.", name);
        }
    }

    private static DateOnlyRange? Dates(OfferingScheduleDefinition definition, int minYear, int maxYear, ValidationErrors errors)
    {
        if (definition.StartDate is { } start && definition.EndDate is { } end)
        {
            var range = DateOnlyRange.Create(start, end);
            if (range.IsFailure)
            {
                errors.Add(range);
                return null;
            }

            if (!InYears(start.Year, minYear, maxYear) || !InYears(end.Year, minYear, maxYear))
            {
                errors.Add(OfferingFields.StartDate, FormattableString.Invariant($"The dates must be between {minYear} and {maxYear}."));
            }

            return range.Value;
        }

        if (definition.StartDate is not null)
        {
            errors.Add(OfferingFields.EndDate, "End date is required when a start date is given.");
        }
        else if (definition.EndDate is not null)
        {
            errors.Add(OfferingFields.StartDate, "Start date is required when an end date is given.");
        }

        return null;
    }

    private static void AvailabilityRules(OfferingAvailability availability, bool dated, int? year, ValidationErrors errors)
    {
        switch (availability)
        {
            case OfferingAvailability.Scheduled or OfferingAvailability.SoldOut:
                if (!dated)
                {
                    errors.Add(OfferingFields.StartDate, "A scheduled or sold-out offering needs its dates.");
                }

                break;
            case OfferingAvailability.OnRequest:
                if (dated)
                {
                    errors.Add(OfferingFields.StartDate, "An offering on request has no dates.");
                }

                if (year is not null)
                {
                    errors.Add(OfferingFields.Year, "An offering on request has no year.");
                }

                break;
            case OfferingAvailability.DatesToBeConfirmed:
                if (!dated && year is null)
                {
                    errors.Add(OfferingFields.Year, "An offering whose dates are to be confirmed needs a year until it has dates.");
                }

                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(availability), availability, "Only a valid availability has date rules.");
        }
    }

    private static void Duration(int? durationDays, DateOnlyRange? dates, int maxDurationDays, ValidationErrors errors)
    {
        if (durationDays is not { } days)
        {
            errors.Add(OfferingFields.DurationDays, "Duration is required.");
        }
        else if (days < 1 || days > maxDurationDays)
        {
            errors.Add(OfferingFields.DurationDays, FormattableString.Invariant($"Duration must be between 1 and {maxDurationDays} days."));
        }
        else if (dates is not null && days != dates.Days)
        {
            errors.Add(OfferingFields.DurationDays, "Duration must equal the number of days from the start to the end date, both included.");
        }
    }

    private static bool InYears(int year, int minYear, int maxYear) => year >= minYear && year <= maxYear;
}
