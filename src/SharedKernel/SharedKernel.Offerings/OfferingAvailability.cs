using System.Diagnostics.CodeAnalysis;

namespace PandaBear.Offerings;

/// <summary>
/// Whether an offering can be booked and on what dates. It is required: the default value is not defined, so a missing availability is a
/// validation error and never means <see cref="Scheduled"/>. Store it by name.
/// </summary>
[SuppressMessage("Design", "CA1008:Enums should have zero value", Justification = "Zero is deliberately undefined, so a missing availability is refused and never read as Scheduled.")]
public enum OfferingAvailability
{
    /// <summary>Fixed dates with places left; the call to action is signing up.</summary>
    Scheduled = 1,

    /// <summary>Fixed dates with no places left; the call to action is contact.</summary>
    SoldOut = 2,

    /// <summary>A planned departure whose dates are not fixed yet: undated with a year, or dated provisionally; the call to action is contact.</summary>
    DatesToBeConfirmed = 3,

    /// <summary>Arranged on request, without dates or a year; the call to action is contact.</summary>
    OnRequest = 4,
}
