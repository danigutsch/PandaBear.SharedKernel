using Microsoft.Data.Sqlite;

namespace PandaBear.Testing.Sqlite;

/// <summary>
/// A private, named in-memory SQLite database for one test: <c>Data Source=file:{name}-{guid}?mode=memory&amp;cache=shared</c>. Every
/// connection opened with <see cref="ConnectionString"/> sees the same database, which lives while this object holds its own connection open.
/// Disposal closes that connection and clears the pool of this connection string, so no idle pooled connection keeps the database (and its
/// memory) alive after the test.
/// </summary>
/// <remarks>
/// Only this database's pool is cleared (<see cref="SqliteConnection.ClearPool(SqliteConnection)"/>), not every pool of the process
/// (<see cref="SqliteConnection.ClearAllPools"/>), so disposing one test's database never disturbs tests that run in parallel. The package
/// references <c>Microsoft.Data.Sqlite.Core</c> only; the test project supplies the native SQLite library, for example through
/// <c>Microsoft.EntityFrameworkCore.Sqlite</c> or <c>Microsoft.Data.Sqlite</c>.
/// </remarks>
public sealed class SqliteInMemoryDatabase : IDisposable
{
    private readonly SqliteConnection _keepAlive;
    private bool _disposed;

    /// <summary>Initializes a new empty database.</summary>
    /// <param name="name">A readable prefix of the database name: letters, digits and dashes.</param>
    /// <exception cref="ArgumentException"><paramref name="name"/> is blank or holds another character.</exception>
    public SqliteInMemoryDatabase(string name = "test")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (!name.All(static character => char.IsAsciiLetterOrDigit(character) || character == '-'))
        {
            throw new ArgumentException("The name may hold only letters, digits and dashes; it becomes part of a URI.", nameof(name));
        }

        ConnectionString = $"Data Source=file:{name}-{Guid.NewGuid():N}?mode=memory&cache=shared";
        _keepAlive = new SqliteConnection(ConnectionString);
        _keepAlive.Open();
    }

    /// <summary>Gets the connection string of the database.</summary>
    public string ConnectionString { get; }

    /// <summary>Closes the connection that keeps the database alive and clears the pooled connections of <see cref="ConnectionString"/>.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        SqliteConnection.ClearPool(_keepAlive);
        _keepAlive.Dispose();
    }
}
