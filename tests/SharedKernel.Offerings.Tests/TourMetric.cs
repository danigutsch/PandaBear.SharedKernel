namespace PandaBear.Offerings.Tests;

/// <summary>ViajantesTurismo V2's tour metrics.</summary>
public enum TourMetric
{
    /// <summary>Distance in kilometres, of the tour and of a day.</summary>
    Distance,

    /// <summary>Kilometres of a day covered by vehicle.</summary>
    Transfer,

    /// <summary>Metres climbed on a day.</summary>
    ElevationGain,

    /// <summary>Metres descended on a day.</summary>
    ElevationLoss,

    /// <summary>Difficulty from 1 to 5.</summary>
    Difficulty,
}
