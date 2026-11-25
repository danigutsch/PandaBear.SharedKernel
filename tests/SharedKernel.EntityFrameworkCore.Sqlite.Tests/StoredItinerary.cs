namespace SharedKernel.EntityFrameworkCore.Sqlite.Tests;

/// <summary>The stored text of an itinerary's JSON columns.</summary>
/// <param name="Cities">The cities column.</param>
/// <param name="Prices">The prices column.</param>
/// <param name="ImageIds">The image ids column.</param>
/// <param name="Stops">The stops column.</param>
public sealed record StoredItinerary(string Cities, string Prices, string ImageIds, string Stops);
