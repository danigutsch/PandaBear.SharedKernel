namespace SharedKernel.Npgsql.Tests;

public abstract class PostgreSqlDatabaseTestBase(PostgreSqlTestServerFixture fixture) : IAsyncLifetime
{
    private PostgreSqlTestDatabaseLease? _database;

    protected NpgsqlDataSource DataSource =>
        _database?.DataSource
        ?? throw new InvalidOperationException("PostgreSQL test database is not initialized.");

    public async ValueTask InitializeAsync()
    {
        _database = await fixture.CreateDatabase(TestContext.Current.CancellationToken);
    }

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
