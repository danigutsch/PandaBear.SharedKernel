using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using PandaBear.Npgsql;
using PandaBear.DispositionHold;
using PandaBear.DispositionHold.EntityFrameworkCore;

namespace PandaBear.EntityFrameworkCore.Tests;

/// <summary>
/// PostgreSQL host of the disposition hold store and guard in an isolated database with the schema created.
/// </summary>
internal sealed class PostgreSqlDispositionHoldScenario : IAsyncDisposable
{
    private readonly PostgreSqlTestDatabaseLease database;
    private readonly NpgsqlDataSource dataSource;
    private readonly ServiceProvider serviceProvider;

    private PostgreSqlDispositionHoldScenario(
        PostgreSqlTestDatabaseLease database,
        NpgsqlDataSource dataSource,
        ServiceProvider serviceProvider)
    {
        this.database = database;
        this.dataSource = dataSource;
        this.serviceProvider = serviceProvider;
    }

    /// <summary>Creates the isolated database, the hold schema and the scenario, cleaning up if creation fails.</summary>
    public static async ValueTask<PostgreSqlDispositionHoldScenario> Create(PostgreSqlFixture fixture, CancellationToken ct)
    {
        var database = await fixture.CreateIsolatedDatabase(ct);
        var dataSource = database.CreateDataSource(nameof(PostgreSqlDispositionHoldScenario));
        var services = new ServiceCollection();
        services.AddDispositionHolds<DispositionHoldTestDbContext>();
        services.AddDbContext<DispositionHoldTestDbContext>(options => options.UseNpgsql(dataSource));
        ServiceProvider? serviceProvider = null;
        try
        {
            serviceProvider = services.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true,
            });

            await using var scope = serviceProvider.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<DispositionHoldTestDbContext>().Database.EnsureCreatedAsync(ct);

            return new PostgreSqlDispositionHoldScenario(database, dataSource, serviceProvider);
        }
        catch (Exception creationFailure)
        {
            await PostgreSqlScenarioCleanup.DisposeResources(creationFailure, serviceProvider, dataSource, database);
            throw;
        }
    }

    /// <summary>Opens a caller with its own context, store, guard and, when asked, an open transaction.</summary>
    public ValueTask<HoldTransaction> Open(
        bool beginTransaction,
        CancellationToken ct,
        IsolationLevel isolationLevel = IsolationLevel.ReadCommitted) =>
        HoldTransaction.Open(serviceProvider, beginTransaction, isolationLevel, ct);

    /// <summary>Reports whether another connection is blocked from taking the advisory lock that guards the scope.</summary>
    public async ValueTask<bool> IsScopeLocked(DispositionScope scope, CancellationToken ct)
    {
        var key = PostgreSqlAdvisoryLockKey.FromUtf8Sha256BigEndian(HoldScopeLock.LockName(scope));
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var command = new NpgsqlCommand("SELECT pg_try_advisory_lock(@key)", connection);
        command.Parameters.AddWithValue("key", key);
        var acquired = (bool)(await command.ExecuteScalarAsync(ct))!;
        if (acquired)
        {
            await using var release = new NpgsqlCommand("SELECT pg_advisory_unlock(@key)", connection);
            release.Parameters.AddWithValue("key", key);
            _ = await release.ExecuteScalarAsync(ct);
        }

        return !acquired;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await PostgreSqlScenarioCleanup.DisposeResources(null, serviceProvider, dataSource, database);
    }
}
