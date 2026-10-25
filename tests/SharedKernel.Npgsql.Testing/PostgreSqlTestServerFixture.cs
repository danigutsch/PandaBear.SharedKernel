using System.Diagnostics.CodeAnalysis;
using Aspire.Hosting.Testing;
using Npgsql;
using Xunit;

namespace PandaBear.Npgsql.Testing;

/// <summary>Leases isolated per-test databases on a PostgreSQL server that an assembly-scoped fixture resolves on demand.</summary>
/// <remarks>
/// <para>
/// Creating and initializing the fixture starts nothing, so a test assembly whose selected tests need no database pays
/// for no container. The first call that needs a database resolves the server: the one that another process owns when the
/// <c>SHAREDKERNEL_TESTING_POSTGRESQL_CONNECTION_STRING</c> environment variable carries its connection string, otherwise a
/// container that <see cref="CreateBuilder"/> composes and the fixture starts and later stops. A set variable always
/// borrows: a malformed value fails the requesting test with a message that names the variable, and an unreachable server
/// fails it too, but neither falls back to a second container. The fixture drops its own databases on disposal and never
/// stops a server it did not start.
/// </para>
/// <para>
/// A failed start is cached, so it fails every test that needs a database instead of repeating a long startup timeout.
/// </para>
/// </remarks>
public abstract class PostgreSqlTestServerFixture : IAsyncLifetime
{
    private readonly PostgreSqlTestServerSource _server;
    private readonly PostgreSqlTestDatabasePool _databases;

    /// <summary>Creates a fixture that finds a server another process owns in the process environment.</summary>
    protected PostgreSqlTestServerFixture()
        : this(Environment.GetEnvironmentVariable)
    {
    }

    internal PostgreSqlTestServerFixture(Func<string, string?> readEnvironment)
    {
        // CreateBuilder runs on the first demand, never while the fixture is constructed.
        _server = new PostgreSqlTestServerSource(CreateBuilder, readEnvironment);
        _databases = new PostgreSqlTestDatabasePool(_server.GetConnectionString);
    }

    /// <summary>Creates the testing builder from the consuming Aspire AppHost assembly.</summary>
    /// <returns>A builder with the consuming assembly's Aspire metadata.</returns>
    /// <remarks>
    /// Override it in the executable test assembly with <see cref="System.Runtime.CompilerServices.MethodImplOptions.NoInlining"/>.
    /// It runs only when this fixture starts a server of its own, on the thread of the first test that needs a database.
    /// </remarks>
    protected abstract IDistributedApplicationTestingBuilder CreateBuilder();

    /// <inheritdoc />
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    internal Task<PostgreSqlTestDatabaseLease> CreateDatabase(CancellationToken ct) => _databases.Create(ct);

    internal IReadOnlyCollection<string> OwnedDatabaseNames => _databases.OwnedDatabaseNames;

    internal Task<string> GetServerConnectionString(CancellationToken ct) => _server.GetConnectionString(ct);

    internal async Task<bool> DatabaseExists(string databaseName, CancellationToken ct)
    {
        var administrativeConnectionString = new NpgsqlConnectionStringBuilder(await _server.GetConnectionString(ct))
        {
            Database = "postgres"
        }.ConnectionString;
        await using var dataSource = NpgsqlDataSource.Create(administrativeConnectionString);
        await using var command = dataSource.CreateCommand(
            "SELECT EXISTS (SELECT 1 FROM pg_database WHERE datname = @databaseName);");
        command.Parameters.AddWithValue("databaseName", databaseName);
        return await command.ExecuteScalarAsync(ct) is true;
    }

    /// <inheritdoc />
    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Assembly teardown must attempt database cleanup and server cleanup before reporting aggregate failures.")]
    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        List<Exception> failures = [];

        try
        {
            await _databases.DisposeAsync();
        }
        catch (Exception exception)
        {
            failures.Add(exception);
        }

        try
        {
            await _server.DisposeAsync();
        }
        catch (Exception exception)
        {
            failures.Add(exception);
        }

        if (failures.Count > 0)
        {
            throw new AggregateException("PostgreSQL test server cleanup failed.", failures);
        }
    }
}
