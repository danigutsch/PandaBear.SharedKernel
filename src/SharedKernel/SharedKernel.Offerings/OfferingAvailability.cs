namespace SharedKernel.Offerings;

/// <summary>
/// Whether an offering can be booked and on what dates. It is required: the default value, <see cref="Unspecified"/>, means "not set", so a
/// missing availability is a validation error and never means <see cref="Scheduled"/>. Store it by name.
/// </summary>
public enum OfferingAvailability
{
    /// <summary>
    /// Not set. It is never a valid state of an offering: validation, restore and every reader refuse it. It is the zero value so that an
    /// unset field or an empty payload is never read as a real availability.
    /// </summary>
    Unspecified = 0,

    /// <summary>Fixed dates with places left; the call to action is signing up.</summary>
    Scheduled = 1,

    /// <summary>Fixed dates with no places left; the call to action is contact.</summary>
    SoldOut = 2,

    /// <summary>A planned departure whose dates are not fixed yet: undated with a year, or dated provisionally; the call to action is contact.</summary>
    DatesToBeConfirmed = 3,

    /// <summary>Arranged on request, without dates or a year; the call to action is contact.</summary>
    OnRequest = 4,
}
