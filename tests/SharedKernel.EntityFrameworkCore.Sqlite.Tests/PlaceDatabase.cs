using Microsoft.EntityFrameworkCore;
using SharedKernel.Testing.Sqlite;

namespace SharedKernel.EntityFrameworkCore.Sqlite.Tests;

/// <summary>A private in-memory database of places whose contexts register the search fold on every connection they open.</summary>
public sealed class PlaceDatabase : IDisposable
{
    private readonly SqliteInMemoryDatabase _database = new("places");

    /// <summary>Creates the schema and stores the places.</summary>
    /// <param name="places">The places.</param>
    public PlaceDatabase(params Place[] places)
    {
        using var context = CreateContext();
        context.Database.EnsureCreated();
        context.Places.AddRange(places);
        context.SaveChanges();
    }

    /// <summary>Creates a context that opens its own connections.</summary>
    /// <returns>A new context.</returns>
    public PlaceDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<PlaceDbContext>().UseSqlite(_database.ConnectionString).AddSearchFold().Options);

    /// <inheritdoc />
    public void Dispose() => _database.Dispose();
}
