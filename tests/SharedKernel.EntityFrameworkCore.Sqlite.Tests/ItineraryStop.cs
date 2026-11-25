namespace SharedKernel.EntityFrameworkCore.Sqlite.Tests;

/// <summary>A small record stored inside a JSON list.</summary>
/// <param name="DayNumber">The day.</param>
/// <param name="Title">The title.</param>
public sealed record ItineraryStop(int DayNumber, string Title);
