namespace SharedKernel.Offerings.Tests;

/// <summary>ViajantesTurismo V2's price kinds, in display order.</summary>
public enum TourPriceKind
{
    /// <summary>Per person in a double room; the base kind.</summary>
    PerPersonDoubleRoom,

    /// <summary>The single-room supplement.</summary>
    SingleRoomSupplement,

    /// <summary>Bike rental.</summary>
    BikeRental,

    /// <summary>E-bike rental.</summary>
    EBikeRental,

    /// <summary>Any other price, with its own label.</summary>
    Other,
}
