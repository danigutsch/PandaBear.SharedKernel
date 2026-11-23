namespace PandaBear.Offerings;

/// <summary>Where a metric may be given: on the whole offering, on each step, or both.</summary>
[Flags]
public enum MetricScope
{
    /// <summary>Nowhere; not valid in a rule.</summary>
    None = 0,

    /// <summary>On the offering, such as a tour's total distance or its difficulty.</summary>
    Offering = 1,

    /// <summary>On a step, such as a day's distance or elevation gain.</summary>
    Step = 2,
}
