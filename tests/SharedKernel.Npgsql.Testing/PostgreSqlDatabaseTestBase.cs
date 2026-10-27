using Npgsql;
using Xunit;

namespace PandaBear.Npgsql.Testing;

/// <summary>Base class for tests that each get their own isolated PostgreSQL database, leased in InitializeAsync and dropped in DisposeAsync.</summary>
/// <param name="fixture">The shared PostgreSQL test server fixture that supplies the per-test database.</param>
public abstract class PostgreSqlDatabaseTestBase(PostgreSqlTestServerFixture fixture) : IAsyncLifetime
{
    private PostgreSqlTestDatabaseLease? _database;

    /// <summary>Gets the data source of the test's leased database, throwing InvalidOperationException before initialization.</summary>
    protected NpgsqlDataSource DataSource =>
        _database?.DataSource
        ?? throw new InvalidOperationException("PostgreSQL test database is not initialized.");

    /// <summary>Gets the connection string of the test's leased database, throwing InvalidOperationException before initialization.</summary>
    protected string ConnectionString =>
        _database?.ConnectionString
        ?? throw new InvalidOperationException("PostgreSQL test database is not initialized.");

    /// <summary>Leases a new isolated database from the fixture for the current test.</summary>
    public async ValueTask InitializeAsync()
    {
        _database = await fixture.CreateDatabase(TestContext.Current.CancellationToken);
    }

    /// <summary>Disposes the leased database, if any, which drops it from the server.</summary>
    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        var database = Interlocked.Exchange(ref _database, null);
        if (database is not null)
        {
            await database.DisposeAsync();
        }
    }
}
