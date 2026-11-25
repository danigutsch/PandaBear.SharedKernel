namespace SharedKernel.Offerings;

/// <summary>How many steps an offering's programme may have.</summary>
public enum OfferingStepCount
{
    /// <summary>Any number up to <see cref="OfferingRules{TPriceKind, TMetric}.MaxSteps"/>.</summary>
    Free = 0,

    /// <summary>None, or exactly one per day of the duration, as a day-by-day itinerary.</summary>
    MatchesDuration = 1,
}
