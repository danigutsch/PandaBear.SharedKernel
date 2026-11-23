namespace PandaBear.Offerings;

/// <summary>Checks availabilities and schedules handed to the readers.</summary>
internal static class OfferingSchedules
{
    /// <summary>Determines whether an availability is a valid state of an offering: defined and not <see cref="OfferingAvailability.Unspecified"/>.</summary>
    /// <param name="availability">The availability.</param>
    /// <returns><see langword="true"/> when it is valid.</returns>
    public static bool IsValid(OfferingAvailability availability) =>
        availability != OfferingAvailability.Unspecified && Enum.IsDefined(availability);

    /// <summary>Refuses a schedule whose availability is not a valid state of an offering.</summary>
    /// <param name="schedule">The schedule.</param>
    /// <param name="name">The parameter name.</param>
    /// <exception cref="ArgumentException">The availability is <see cref="OfferingAvailability.Unspecified"/> or not defined.</exception>
    public static void Check(OfferingSchedule schedule, string name)
    {
        if (!IsValid(schedule.Availability))
        {
            throw new ArgumentException("The schedule's availability is not set or not defined.", name);
        }
    }
}
