using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace PandaBear.EntityFrameworkCore.Sqlite.Tests;

/// <summary>A private in-memory SQLite database with the schema created.</summary>
public sealed class SqliteTestDatabase : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    /// <summary>Opens the database and creates the schema.</summary>
    public SqliteTestDatabase()
    {
        _connection.Open();
        using var context = CreateContext();
        context.Database.EnsureCreated();
    }

    /// <summary>Creates a context on the shared connection.</summary>
    /// <returns>A new context.</returns>
    public AccountDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AccountDbContext>().UseSqlite(_connection).Options);

    /// <inheritdoc />
    public void Dispose() => _connection.Dispose();
}
