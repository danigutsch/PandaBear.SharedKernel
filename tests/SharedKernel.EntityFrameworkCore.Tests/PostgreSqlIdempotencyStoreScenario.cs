using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using PandaBear.Idempotency;
using PandaBear.Idempotency.EntityFrameworkCore;

namespace PandaBear.EntityFrameworkCore.Tests;

/// <summary>
/// PostgreSQL-backed idempotency store host whose save interceptor holds two concurrent TryStart calls until both are saving.
/// </summary>
internal sealed class PostgreSqlIdempotencyStoreScenario : IAsyncDisposable
{
    private readonly PostgreSqlTestDatabaseLease database;
    private readonly NpgsqlDataSource dataSource;
    private readonly ConcurrentSaveBarrierInterceptor saveBarrier;
    private readonly ServiceProvider serviceProvider;

    private PostgreSqlIdempotencyStoreScenario(
        PostgreSqlTestDatabaseLease database,
        NpgsqlDataSource dataSource,
        ConcurrentSaveBarrierInterceptor saveBarrier,
        ServiceProvider serviceProvider)
    {
        this.database = database;
        this.dataSource = dataSource;
        this.saveBarrier = saveBarrier;
        this.serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Creates an isolated PostgreSQL database with the idempotency store, its context and the save barrier, creates the schema and returns the scenario, cleaning up if creation fails.
    /// </summary>
    public static async ValueTask<PostgreSqlIdempotencyStoreScenario> Create(
        PostgreSqlFixture fixture,
        CancellationToken ct)
    {
        var database = await fixture.CreateIsolatedDatabase(ct);
        var dataSource = database.CreateDataSource(nameof(PostgreSqlIdempotencyStoreScenario));
        var saveBarrier = new ConcurrentSaveBarrierInterceptor();
        var services = new ServiceCollection();
        services.AddSingleton(saveBarrier);
        services.AddIdempotencyStore<IdempotencyDbContext>();
        services.AddDbContext<IdempotencyDbContext>((serviceProvider, options) =>
        {
            options.UseNpgsql(dataSource);
            options.AddInterceptors(serviceProvider.GetRequiredService<ConcurrentSaveBarrierInterceptor>());
        });
        ServiceProvider? serviceProvider = null;
        try
        {
            serviceProvider = services.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true,
            });

            await using var scope = serviceProvider.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<IdempotencyDbContext>();
            await dbContext.Database.EnsureCreatedAsync(ct);

            return new PostgreSqlIdempotencyStoreScenario(database, dataSource, saveBarrier, serviceProvider);
        }
        catch (Exception creationFailure)
        {
            await PostgreSqlScenarioCleanup.DisposeResources(creationFailure, serviceProvider, dataSource, database);
            throw;
        }
    }

    /// <summary>
    /// Starts the same operation from two scopes at once, releases both saves together and returns both results.
    /// </summary>
    public async ValueTask<IdempotencyStartResult[]> TryStartConcurrently(CancellationToken ct)
    {
        var operation = new IdempotencyOperation(
            IdempotencyScope.From("catalog.integration-event.admin.tour.created.v1"),
            IdempotencyKey.From(Guid.CreateVersion7().ToString("N")));
        var startedAt = new DateTimeOffset(2026, 7, 20, 12, 0, 0, TimeSpan.Zero);
        await using var firstScope = serviceProvider.CreateAsyncScope();
        await using var secondScope = serviceProvider.CreateAsyncScope();
        var firstStore = firstScope.ServiceProvider.GetRequiredService<IIdempotencyStore>();
        var secondStore = secondScope.ServiceProvider.GetRequiredService<IIdempotencyStore>();

        var firstStart = firstStore.TryStart(operation, startedAt, TimeSpan.FromMinutes(5), ct).AsTask();
        var secondStart = secondStore.TryStart(operation, startedAt, TimeSpan.FromMinutes(5), ct).AsTask();
        await saveBarrier.BothSaving.WaitAsync(ct);
        saveBarrier.Release();

        return await Task.WhenAll(firstStart, secondStart);
    }

    /// <summary>
    /// Starts a new entry in the scope with the owner and start time and returns its operation. Opens the save barrier for good, so it suits scenarios that do not force concurrent saves.
    /// </summary>
    public async ValueTask<IdempotencyOperation> Seed(string scopeValue, string? retentionOwner, DateTimeOffset startedAt, CancellationToken ct)
    {
        saveBarrier.Release();
        var operation = new IdempotencyOperation(
            IdempotencyScope.From(scopeValue),
            IdempotencyKey.From(Guid.CreateVersion7().ToString("N")));
        await using var scope = serviceProvider.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IIdempotencyStore>();
        (await store.TryStart(operation, startedAt, TimeSpan.FromMinutes(5), retentionOwner, ct)).Started.ShouldBeTrue();
        return operation;
    }

    /// <summary>
    /// Completes the operation at the given time.
    /// </summary>
    public async ValueTask Complete(IdempotencyOperation operation, DateTimeOffset completedAt, CancellationToken ct)
    {
        saveBarrier.Release();
        await using var scope = serviceProvider.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IIdempotencyStore>();
        await store.Complete(operation, completedAt, ct);
    }

    /// <summary>
    /// Reads the stored entry of the operation, or null when it was deleted.
    /// </summary>
    public async ValueTask<IdempotencyEntry?> Find(IdempotencyOperation operation, CancellationToken ct)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IIdempotencyStore>();
        return await store.Get(operation, ct);
    }

    /// <summary>
    /// Runs one retention purge batch through the registered retention store.
    /// </summary>
    public async ValueTask<IdempotencyRetentionPurgeResult> Purge(
        DateTimeOffset cutoff,
        string? ownerTrackedScopePrefix,
        string[] heldRetentionOwners,
        int batchSize,
        CancellationToken ct,
        string? scopePrefix = null)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var retention = scope.ServiceProvider.GetRequiredService<IIdempotencyRetentionStore>();
        return await retention.PurgeExpired(cutoff, ownerTrackedScopePrefix, heldRetentionOwners, batchSize, ct, scopePrefix);
    }

    /// <summary>
    /// Counts the stored idempotency entries.
    /// </summary>
    public async ValueTask<int> CountEntries(CancellationToken ct)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IdempotencyDbContext>();
        return await dbContext.Set<IdempotencyEntryEntity>().CountAsync(ct);
    }

    /// <summary>
    /// Releases the provider, data source and isolated database.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        await PostgreSqlScenarioCleanup.DisposeResources(null, serviceProvider, dataSource, database);
    }

    private sealed class IdempotencyDbContext(
        DbContextOptions<IdempotencyDbContext> options,
        IEnumerable<IDbContextConfiguration<IdempotencyDbContext>> configurations) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            foreach (var configuration in configurations)
            {
                configuration.ConfigureModel(modelBuilder);
            }
        }
    }
}
