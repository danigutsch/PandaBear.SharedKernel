namespace SharedKernel.EntityFrameworkCore.Sqlite.Tests;

/// <summary>An entity whose ordered lists are stored as JSON text columns.</summary>
public sealed class Itinerary
{
    /// <summary>Gets or sets the key.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Gets or sets the city names.</summary>
    public IReadOnlyList<string> Cities { get; set; } = [];

    /// <summary>Gets or sets the price amounts.</summary>
    public IReadOnlyList<decimal> Prices { get; set; } = [];

    /// <summary>Gets or sets the image ids.</summary>
    public IReadOnlyList<Guid> ImageIds { get; set; } = [];

    /// <summary>Gets or sets the days.</summary>
    public IReadOnlyList<ItineraryStop> Stops { get; set; } = [];

    /// <summary>Gets or sets the tags; declared nullable so only the mapping makes the column required.</summary>
    public IReadOnlyList<string>? Tags { get; set; } = [];
}
