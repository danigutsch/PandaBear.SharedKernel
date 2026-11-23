using PandaBear.BuildingBlocks;

namespace PandaBear.Offerings;

/// <summary>When an offering runs: its availability, its days when fixed, its year and its length in days.</summary>
/// <param name="Availability">The availability.</param>
/// <param name="Dates">The first and last day, both included; <see langword="null"/> when undated.</param>
/// <param name="Year">The year: the first day's year when dated, the entered year of an undated offering whose dates are to be confirmed, else <see langword="null"/>.</param>
/// <param name="DurationDays">The length in days, counting the first and the last day; it equals <see cref="DateOnlyRange.Days"/> when dated.</param>
public sealed record OfferingSchedule(OfferingAvailability Availability, DateOnlyRange? Dates, int? Year, int DurationDays)
{
    /// <summary>Gets the number of nights, one fewer than the days.</summary>
    public int Nights => DurationDays - 1;
}
