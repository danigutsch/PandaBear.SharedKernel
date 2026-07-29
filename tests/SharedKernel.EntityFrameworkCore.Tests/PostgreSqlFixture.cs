using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Npgsql;
using SharedKernel.IntegrationTesting;

namespace SharedKernel.EntityFrameworkCore.Tests;

/// <summary>
/// Provides one lazily started PostgreSQL server and isolated databases for this test assembly.
/// </summary>
public sealed class PostgreSqlFixture : IAsyncLifetime
{
    private const string DatabaseResourceName = "fixture";
    private const string PostgreSqlResourceName = "postgres";

    private readonly ConcurrentDictionary<string, byte> ownedDatabases = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim startupGate = new(1, 1);
    private AspireTestApplication? app;
    private string? databaseConnectionString;

    /// <inheritdoc />
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    internal async Task<PostgreSqlTestDatabase> CreateIsolatedDatabase(CancellationToken ct)
    {
        var databaseName = $"{PostgreSqlTestDatabase.DatabaseNamePrefix}{Guid.NewGuid():N}";
        return await CreateIsolatedDatabase(databaseName, ct);
    }

    internal async Task<PostgreSqlTestDatabase> CreateIsolatedDatabase(
        string databaseName,
        CancellationToken ct)
    {
        var connectionString = await GetDatabaseConnectionString(ct);
        if (!ownedDatabases.TryAdd(databaseName, 0))
        {
            throw new InvalidOperationException("A duplicate PostgreSQL test database was registered.");
        }

        return await PostgreSqlTestDatabase.Create(connectionString, databaseName, OnDatabaseDropped, ct);
    }

    internal bool OwnsDatabase(string databaseName) => ownedDatabases.ContainsKey(databaseName);

    internal async Task<bool> DatabaseExists(string databaseName, CancellationToken ct)
    {
        await using var dataSource = NpgsqlDataSource.Create(await GetDatabaseConnectionString(ct));
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
        List<Exception> failures = [];
        var connectionString = databaseConnectionString;
        databaseConnectionString = null;
        if (connectionString is not null)
        {
            foreach (var databaseName in ownedDatabases.Keys)
            {
                try
                {
                    await PostgreSqlTestDatabase.DropDatabase(connectionString, databaseName);
                    _ = ownedDatabases.TryRemove(databaseName, out _);
                }
                catch (Exception exception)
                {
                    failures.Add(exception);
                }
            }
        }

        var application = Interlocked.Exchange(ref app, null);
        try
        {
            if (application is not null)
            {
                try
                {
                    await application.DisposeAsync();
                }
                catch (Exception exception)
                {
                    failures.Add(exception);
                }
            }
        }
        finally
        {
            startupGate.Dispose();
        }

        if (failures.Count > 0)
        {
            throw new AggregateException("PostgreSQL test fixture cleanup failed.", failures);
        }
    }

    private async Task<string> GetDatabaseConnectionString(CancellationToken ct)
    {
        var currentConnectionString = Volatile.Read(ref databaseConnectionString);
        if (currentConnectionString is not null)
        {
            return currentConnectionString;
        }

        await startupGate.WaitAsync(ct);
        try
        {
            currentConnectionString = databaseConnectionString;
            if (currentConnectionString is not null)
            {
                return currentConnectionString;
            }

            var appBuilder = AspireTestApplication.CreateBuilder();
            var databaseServer = appBuilder.AddPostgres(PostgreSqlResourceName);
            _ = databaseServer.AddDatabase(DatabaseResourceName);

            var application = await AspireTestApplication.Start(
                appBuilder,
                [PostgreSqlResourceName],
                resourceStartupTimeout: null,
                ct);
            try
            {
                currentConnectionString = await application.GetConnectionString(DatabaseResourceName, ct);
                app = application;
                Volatile.Write(ref databaseConnectionString, currentConnectionString);
                return currentConnectionString;
            }
            catch (Exception startupFailure)
            {
                await PostgreSqlScenarioCleanup.DisposeResources(startupFailure, application);
                throw;
            }
        }
        finally
        {
            _ = startupGate.Release();
        }
    }

    private void OnDatabaseDropped(string databaseName)
    {
        _ = ownedDatabases.TryRemove(databaseName, out _);
    }
}
