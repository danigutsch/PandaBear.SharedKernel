using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Aspire.Hosting;
using Aspire.Hosting.Testing;
using Npgsql;
using PandaBear.IntegrationTesting;
using Xunit;

namespace PandaBear.Npgsql.Testing;

/// <summary>Owns an assembly-scoped PostgreSQL server with isolated per-test database leases.</summary>
public abstract class PostgreSqlTestServerFixture : IAsyncLifetime
{
    private const string DatabaseResourceName = "fixture";
    private const string PostgreSqlResourceName = "postgres";

    private readonly ConcurrentDictionary<string, byte> _ownedDatabases = new(StringComparer.Ordinal);
    private AspireTestApplication? _app;
    private string? _administrativeConnectionString;

    /// <summary>Creates the testing builder from the consuming Aspire AppHost assembly.</summary>
    /// <returns>A builder with the consuming assembly's Aspire metadata.</returns>
    protected abstract IDistributedApplicationTestingBuilder CreateBuilder();

    /// <inheritdoc />
    public async ValueTask InitializeAsync()
    {
        var appBuilder = CreateBuilder();
        var databaseServer = appBuilder.AddPostgres(PostgreSqlResourceName);
        _ = databaseServer.AddDatabase(DatabaseResourceName);

        var app = await AspireTestApplication.Start(
            appBuilder,
            [PostgreSqlResourceName],
            resourceStartupTimeout: null,
            TestContext.Current.CancellationToken);
        try
        {
            var connectionString = await app.GetConnectionString(
                DatabaseResourceName,
                TestContext.Current.CancellationToken);
            _administrativeConnectionString = new NpgsqlConnectionStringBuilder(connectionString)
            {
                Database = "postgres"
            }.ConnectionString;
            _app = app;
        }
        catch (Exception initializationFailure)
        {
            await PostgreSqlTestCleanup.Run(initializationFailure, app.DisposeAsync);
            throw;
        }
    }

    internal async Task<PostgreSqlTestDatabaseLease> CreateDatabase(CancellationToken ct)
    {
        var connectionString = GetAdministrativeConnectionString();
        var databaseName = $"test_{Guid.NewGuid():N}";
        if (!_ownedDatabases.TryAdd(databaseName, 0))
        {
            throw new InvalidOperationException("A duplicate PostgreSQL test database lease was issued.");
        }

        return await PostgreSqlTestDatabaseLease.Create(
            connectionString,
            databaseName,
            OnDatabaseDropped,
            ct);
    }

    internal async Task<bool> DatabaseExists(string databaseName, CancellationToken ct)
    {
        await using var dataSource = NpgsqlDataSource.Create(GetAdministrativeConnectionString());
        await using var command = dataSource.CreateCommand(
            "SELECT EXISTS (SELECT 1 FROM pg_database WHERE datname = @databaseName);");
        command.Parameters.AddWithValue("databaseName", databaseName);
        return await command.ExecuteScalarAsync(ct) is true;
    }

    /// <inheritdoc />
    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Assembly teardown must attempt every owned database and Aspire cleanup before reporting aggregate failures.")]
    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        List<Exception> failures = [];
        var connectionString = _administrativeConnectionString;
        _administrativeConnectionString = null;

        if (connectionString is not null)
        {
            foreach (var databaseName in _ownedDatabases.Keys)
            {
                try
                {
                    await PostgreSqlTestDatabaseLease.DropDatabase(connectionString, databaseName);
                    _ = _ownedDatabases.TryRemove(databaseName, out _);
                }
                catch (Exception exception)
                {
                    failures.Add(exception);
                }
            }
        }

        var app = Interlocked.Exchange(ref _app, null);
        if (app is not null)
        {
            try
            {
                await app.DisposeAsync();
            }
            catch (Exception exception)
            {
                failures.Add(exception);
            }
        }

        if (failures.Count > 0)
        {
            throw new AggregateException("PostgreSQL test server cleanup failed.", failures);
        }
    }

    private string GetAdministrativeConnectionString()
    {
        return _administrativeConnectionString
            ?? throw new InvalidOperationException("PostgreSQL test server fixture is not initialized.");
    }

    private void OnDatabaseDropped(string databaseName)
    {
        _ = _ownedDatabases.TryRemove(databaseName, out _);
    }
}
