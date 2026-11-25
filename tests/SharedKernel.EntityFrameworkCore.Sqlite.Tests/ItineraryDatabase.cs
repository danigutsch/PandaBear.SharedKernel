using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace SharedKernel.EntityFrameworkCore.Sqlite.Tests;

/// <summary>A private in-memory database of itineraries.</summary>
public sealed class ItineraryDatabase : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    /// <summary>Opens the database and creates the schema.</summary>
    public ItineraryDatabase()
    {
        _connection.Open();
        using var context = CreateContext();
        context.Database.EnsureCreated();
    }

    /// <summary>Creates a context on the shared connection.</summary>
    /// <returns>A new context.</returns>
    public ItineraryDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<ItineraryDbContext>().UseSqlite(_connection).Options);

    /// <summary>Stores an itinerary through its own context.</summary>
    /// <param name="itinerary">The itinerary.</param>
    /// <returns>A task that completes when it is stored.</returns>
    public async Task Store(Itinerary itinerary)
    {
        ArgumentNullException.ThrowIfNull(itinerary);

        await using var context = CreateContext();
        context.Itineraries.Add(itinerary);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Reads the stored text of an itinerary's columns.</summary>
    /// <param name="id">The key.</param>
    /// <returns>The stored text.</returns>
    public async Task<StoredItinerary> Stored(Guid id)
    {
        await using var context = CreateContext();
        return await context.Database
            .SqlQuery<StoredItinerary>($"SELECT Cities, Prices, ImageIds, Stops FROM Itineraries WHERE Id = {id}")
            .SingleAsync(TestContext.Current.CancellationToken);
    }

    /// <inheritdoc />
    public void Dispose() => _connection.Dispose();
}
