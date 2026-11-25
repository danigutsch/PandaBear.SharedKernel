namespace SharedKernel.EntityFrameworkCore.Sqlite.Tests;

/// <summary>Sample itineraries for the JSON column tests.</summary>
public static class JsonColumnSamples
{
    /// <summary>Creates an itinerary with two items in every list.</summary>
    /// <returns>A new itinerary.</returns>
    public static Itinerary Full() => new()
    {
        Cities = ["Lisboa", "São Paulo"],
        Prices = [1250.50m, 99m],
        ImageIds = [Guid.Parse("0f8fad5b-d9cb-469f-a165-70867728950e"), Guid.Parse("7c9e6679-7425-40de-944b-e07fc1f90ae7")],
        Stops = [new ItineraryStop(1, "Arrival"), new ItineraryStop(2, "Ride")],
    };
}
