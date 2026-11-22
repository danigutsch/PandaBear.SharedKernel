namespace PandaBear.Offerings;

/// <summary>Unvalidated schedule of an offering, as an editor entered it.</summary>
/// <param name="Availability">The availability; the default value is not defined and is refused as missing.</param>
/// <param name="StartDate">The first day; given together with <paramref name="EndDate"/> or not at all.</param>
/// <param name="EndDate">The last day, not before the first.</param>
/// <param name="Year">The year of an undated offering whose dates are to be confirmed; refused otherwise, because a dated offering takes it from its dates.</param>
/// <param name="DurationDays">The length in days; required, and equal to the dates' day count when dated.</param>
public sealed record OfferingScheduleDefinition(
    OfferingAvailability Availability,
    DateOnly? StartDate = null,
    DateOnly? EndDate = null,
    int? Year = null,
    int? DurationDays = null);
