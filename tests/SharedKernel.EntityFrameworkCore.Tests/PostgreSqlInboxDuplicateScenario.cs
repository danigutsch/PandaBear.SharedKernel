using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using PandaBear.Idempotency;
using PandaBear.Idempotency.EntityFrameworkCore;
using PandaBear.Messaging.IntegrationEvents.EntityFrameworkCore;

namespace PandaBear.EntityFrameworkCore.Tests;

/// <summary>Hosts independent inbox operations in a fixture-owned PostgreSQL database.</summary>
internal sealed class PostgreSqlInboxDuplicateScenario(
    PostgreSqlTestDatabaseLease database, NpgsqlDataSource dataSource, ServiceProvider provider,
    ConcurrentQueue<string> commands) : IAsyncDisposable
{
    /// <summary>The executed PostgreSQL operations whose SQL and bounded projection can be inspected.</summary>
    internal ConcurrentQueue<string> Commands => commands;

    /// <summary>Builds an inbox operation for an event handled by the selected scope.</summary>
    internal static IdempotencyOperation Operation(string scope, Guid eventId) =>
        new(IdempotencyScope.From(scope), IdempotencyKey.From(eventId.ToString("N")));

    /// <summary>Creates a database using a custom inbox schema and parent table name.</summary>
    internal static async Task<PostgreSqlInboxDuplicateScenario> Create(
        PostgreSqlFixture fixture, CancellationToken ct, bool enableObservations = true, bool initializeLegacy = false,
        string observationTable = "observations", string? overlappingPrefix = null)
    {
        var database = await fixture.CreateIsolatedDatabase(ct);
        NpgsqlDataSource? dataSource = null;
        ServiceProvider? provider = null;
        try
        {
            dataSource = database.CreateDataSource(nameof(PostgreSqlInboxDuplicateScenario));
            var services = new ServiceCollection();
            services.AddIntegrationEventInbox<ConsumerTransportDbContext>(ConfigureInbox);
            if (enableObservations)
            {
                services.AddPostgreSqlIntegrationEventInboxDuplicates<ConsumerTransportDbContext>(
                    "gallery", "gallery.integration-event.", observationTable);
                services.AddPostgreSqlIntegrationEventInboxDuplicates<ConsumerTransportDbContext>(
                    "media", "media.integration-event.", observationTable);
                if (overlappingPrefix is not null)
                {
                    services.AddPostgreSqlIntegrationEventInboxDuplicates<ConsumerTransportDbContext>(
                        "another-consumer", overlappingPrefix, observationTable);
                }
            }

            var commands = new ConcurrentQueue<string>();
            services.AddDbContext<ConsumerTransportDbContext>(options => options.UseNpgsql(dataSource)
                .EnableServiceProviderCaching(false)
                .LogTo(commands.Enqueue, [RelationalEventId.CommandExecuted]));
            if (initializeLegacy)
            {
                services.AddIntegrationEventInbox<LegacyInboxContext>(ConfigureInbox);
                services.AddDbContext<LegacyInboxContext>(options => options.UseNpgsql(dataSource));
                services.UseDefaultIdempotencyStore<ConsumerTransportDbContext>();
            }

            provider = services.BuildServiceProvider();
            await using var scope = provider.CreateAsyncScope();
            DbContext context = initializeLegacy
                ? scope.ServiceProvider.GetRequiredService<LegacyInboxContext>()
                : scope.ServiceProvider.GetRequiredService<ConsumerTransportDbContext>();
            await context.Database.EnsureCreatedAsync(ct);
            return new PostgreSqlInboxDuplicateScenario(database, dataSource, provider, commands);
        }
        catch (Exception failure)
        {
            await PostgreSqlScenarioCleanup.DisposeResources(failure, provider, dataSource, database);
            throw;
        }
    }

    /// <summary>Attempts to activate a consumer binding with the supplied configuration.</summary>
    internal static void RegisterObservationTable(string consumerName, string scopePrefix, string tableName) =>
        new ServiceCollection().AddPostgreSqlIntegrationEventInboxDuplicates<ConsumerTransportDbContext>(
            consumerName, scopePrefix, tableName);

    /// <summary>Attempts to record an observation using an unrelated EF Core provider.</summary>
    internal static async Task RecordWithInMemoryProvider(CancellationToken ct)
    {
        var services = new ServiceCollection();
        services.AddPostgreSqlIntegrationEventInboxDuplicates<ConsumerTransportDbContext>("consumer", "inbox.");
        services.AddDbContext<ConsumerTransportDbContext>(options => options
            .UseInMemoryDatabase(nameof(PostgreSqlInboxDuplicateScenario)).EnableServiceProviderCaching(false));
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var recorder = scope.ServiceProvider.GetRequiredService<IntegrationEventInboxDuplicateRecorder<ConsumerTransportDbContext>>();
        await recorder.Record(IdempotencyScope.From("inbox.event"), Guid.CreateVersion7(), DateTimeOffset.UnixEpoch, ct);
    }

    /// <summary>Persists an operation with the legacy model and applies the opt-in context's generated migration SQL.</summary>
    internal async Task<IReadOnlyList<MigrationOperation>> CompleteLegacyAndMigrate(
        IdempotencyOperation operation, DateTimeOffset completedAt, CancellationToken ct)
    {
        await using var scope = provider.CreateAsyncScope();
        var legacy = scope.ServiceProvider.GetRequiredService<LegacyInboxContext>();
        var context = scope.ServiceProvider.GetRequiredService<ConsumerTransportDbContext>();
        var store = scope.ServiceProvider.GetRequiredKeyedService<IIdempotencyStore>(typeof(LegacyInboxContext));
        await store.TryStart(operation, completedAt, TimeSpan.FromMinutes(5), ct);
        await store.Complete(operation, completedAt, "legacy fingerprint", ct);
        var source = legacy.GetService<IDesignTimeModel>().Model;
        var target = context.GetService<IDesignTimeModel>().Model;
        var changes = context.GetService<IMigrationsModelDiffer>()
            .GetDifferences(source.GetRelationalModel(), target.GetRelationalModel());
        var commandsToApply = context.GetService<IMigrationsSqlGenerator>().Generate(changes, target);
        foreach (var command in commandsToApply)
        {
            await context.Database.ExecuteSqlRawAsync(command.CommandText, ct);
        }

        return changes;
    }

    /// <summary>Attempts to start an inbox operation from an independent scope.</summary>
    internal async Task<IdempotencyStartResult> TryStart(
        IdempotencyOperation operation, DateTimeOffset startedAt, CancellationToken ct)
    {
        await using var scope = provider.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IIdempotencyStore>();
        return await store.TryStart(operation, startedAt, TimeSpan.FromMinutes(5), ct);
    }

    /// <summary>Starts and completes an inbox operation.</summary>
    internal async Task Complete(
        IdempotencyOperation operation, DateTimeOffset completedAt, CancellationToken ct, string? owner = null)
    {
        await using var scope = provider.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IIdempotencyStore>();
        await store.TryStart(operation, completedAt, TimeSpan.FromMinutes(5), owner, ct);
        await store.Complete(operation, completedAt, "private fingerprint", ct);
    }

    /// <summary>Reads the parent independently of the observation store.</summary>
    internal async Task<IdempotencyEntry?> ReadEntry(IdempotencyOperation operation, CancellationToken ct)
    {
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IIdempotencyStore>().Get(operation, ct);
    }

    /// <summary>Reads the original PostgreSQL parent row version.</summary>
    internal async Task<long> ReadEntryVersion(IdempotencyOperation operation, CancellationToken ct)
    {
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ConsumerTransportDbContext>();
        return await context.Database.SqlQuery<long>($"""
            SELECT xmin::text::bigint AS "Value" FROM custom_inbox.operations
            WHERE "Scope" = {operation.Scope.Value} AND "Key" = {operation.Key.Value}
            """).SingleAsync(ct);
    }

    /// <summary>Records the completed observation in a separate scoped context.</summary>
    internal async Task<bool> Record(
        IdempotencyScope inboxScope, Guid eventId, DateTimeOffset observedAt, CancellationToken ct)
    {
        await using var scope = provider.CreateAsyncScope();
        var recorder = scope.ServiceProvider.GetRequiredService<IntegrationEventInboxDuplicateRecorder<ConsumerTransportDbContext>>();
        return await recorder.Record(inboxScope, eventId, observedAt, ct);
    }

    /// <summary>Reads one bounded page of metadata.</summary>
    internal async Task<IntegrationEventInboxDuplicatePage> ReadPage(
        string consumer, CancellationToken ct, Guid? eventId = null, int limit = 20,
        IntegrationEventInboxDuplicateCursor? cursor = null)
    {
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ConsumerTransportDbContext>();
        return await IntegrationEventInboxDuplicates.ReadPage(context, consumer, eventId, limit, cursor, ct);
    }

    /// <summary>Reads one bounded page of metadata using the requested observation cutoff.</summary>
    internal async Task<IntegrationEventInboxDuplicatePage> ReadPage(
        string consumer, DateTimeOffset? observedSince, Guid? eventId, int limit,
        IntegrationEventInboxDuplicateCursor? cursor, CancellationToken ct)
    {
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ConsumerTransportDbContext>();
        return await IntegrationEventInboxDuplicates.ReadPage(context, consumer, eventId, observedSince, limit, cursor, ct);
    }

    /// <summary>Reads the selected consumer's retained duplicate aggregate.</summary>
    internal async Task<IntegrationEventInboxDuplicateHealth> ReadHealth(string consumer, CancellationToken ct)
    {
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ConsumerTransportDbContext>();
        return await IntegrationEventInboxDuplicates.ReadHealth(context, consumer, ct);
    }

    /// <summary>Purges through the existing retained idempotency cleanup path.</summary>
    internal async Task<IdempotencyRetentionPurgeResult> Purge(
        DateTimeOffset cutoff, string prefix, IReadOnlyCollection<string> heldOwners, CancellationToken ct)
    {
        await using var scope = provider.CreateAsyncScope();
        var retention = scope.ServiceProvider.GetRequiredService<IIdempotencyRetentionStore>();
        return await retention.PurgeExpired(cutoff, prefix, heldOwners, batchSize: 10, ct, prefix);
    }

    /// <summary>Moves the raw storage counter to its representable limit.</summary>
    internal async Task Saturate(Guid eventId, CancellationToken ct)
    {
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ConsumerTransportDbContext>();
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE custom_inbox.observations SET "DuplicateCount" = {int.MaxValue} WHERE "EventId" = {eventId}
            """, ct);
    }

    /// <summary>Records in a rollback-only transaction and leaves the scoped caller's state untouched.</summary>
    internal async Task<bool> RecordAndRollback(
        IdempotencyScope inboxScope, Guid eventId, DateTimeOffset observedAt, CancellationToken ct)
    {
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ConsumerTransportDbContext>();
        await using var transaction = await context.Database.BeginTransactionAsync(ct);
        var recorder = scope.ServiceProvider.GetRequiredService<IntegrationEventInboxDuplicateRecorder<ConsumerTransportDbContext>>();
        var recorded = await recorder.Record(inboxScope, eventId, observedAt, ct);
        await transaction.RollbackAsync(ct);
        return recorded;
    }

    /// <summary>Cancels an observation after PostgreSQL has blocked its write on the retained parent row.</summary>
    internal async Task<bool> CancelBlockedObservation(
        IdempotencyOperation operation, Guid eventId, DateTimeOffset observedAt, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await using var hold = connection.CreateCommand();
        hold.Transaction = transaction;
        hold.CommandText = """
            SELECT 1 FROM custom_inbox.operations WHERE "Scope" = @scope AND "Key" = @key FOR UPDATE
            """;
        hold.Parameters.AddWithValue("scope", operation.Scope.Value);
        hold.Parameters.AddWithValue("key", operation.Key.Value);
        (await hold.ExecuteScalarAsync(ct)).ShouldNotBeNull();

        using var cancelRecord = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var write = Record(operation.Scope, eventId, observedAt, cancelRecord.Token);
        var blocked = false;
        try
        {
            blocked = await IsWriterBlocked(connection.ProcessID, ct);
        }
        finally
        {
            await cancelRecord.CancelAsync();
            Func<Task> canceledWrite = () => write;
            await canceledWrite.ShouldThrowAssignableTo<OperationCanceledException>();
        }

        return blocked;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync() =>
        await PostgreSqlScenarioCleanup.DisposeResources(null, provider, dataSource, database);

    private static void ConfigureInbox(IdempotencyStorageOptions options)
    {
        options.Schema = "custom_inbox";
        options.TableName = "operations";
    }

    private async Task<bool> IsWriterBlocked(int blockingProcessId, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT EXISTS (
                SELECT 1 FROM pg_stat_activity
                WHERE datname = current_database() AND @blocker = ANY(pg_blocking_pids(pid))
            )
            """;
        command.Parameters.AddWithValue("blocker", blockingProcessId);
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(ct);
        limit.CancelAfter(TimeSpan.FromSeconds(10));
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(20));
        try
        {
            while (await timer.WaitForNextTickAsync(limit.Token))
            {
                if (await command.ExecuteScalarAsync(limit.Token) is true)
                {
                    return true;
                }
            }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // The caller can still cancel and observe the concurrent write on timeout.
        }

        return false;
    }

    private sealed class LegacyInboxContext(
        DbContextOptions<LegacyInboxContext> options,
        IEnumerable<IDbContextConfiguration<LegacyInboxContext>> configurations) : DbContext(options)
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
