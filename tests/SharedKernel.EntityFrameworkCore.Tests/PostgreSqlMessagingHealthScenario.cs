using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using SharedKernel.Messaging.IntegrationEvents.EntityFrameworkCore;

namespace SharedKernel.EntityFrameworkCore.Tests;

/// <summary>Seeds and reads a fixture-owned database with custom outbox and transport mappings.</summary>
/// <param name="database">The isolated test database.</param>
/// <param name="dataSource">The data source owned by the scenario.</param>
/// <param name="provider">The configured EF Core services.</param>
/// <param name="commands">The PostgreSQL commands recorded by EF Core.</param>
internal sealed class PostgreSqlMessagingHealthScenario(
    PostgreSqlTestDatabaseLease database, NpgsqlDataSource dataSource, ServiceProvider provider, List<string> commands) : IAsyncDisposable
{
    private const string OutboxSchema = "health_outbox";
    private const string OutboxTable = "operations_outbox";

    /// <summary>Gets the commands executed for the most recent read.</summary>
    internal IReadOnlyList<string> ReadCommands => commands;

    /// <summary>Creates and initializes a fresh, isolated database.</summary>
    internal static async Task<PostgreSqlMessagingHealthScenario> Create(
        PostgreSqlFixture fixture, CancellationToken ct, bool useDefaultOutbox = false, bool initializeLegacy = false)
    {
        var database = await fixture.CreateIsolatedDatabase(ct);
        NpgsqlDataSource? dataSource = null;
        ServiceProvider? provider = null;
        try
        {
            dataSource = database.CreateDataSource(nameof(PostgreSqlMessagingHealthScenario));
            var services = new ServiceCollection();
            void ConfigureStorage(IntegrationEventStorageOptions options)
            {
                if (!useDefaultOutbox)
                {
                    options.OutboxSchema = OutboxSchema;
                    options.OutboxTableName = OutboxTable;
                }
                options.TransportSchema = "health_transport";
                options.TransportTableName = "operations_transport";
            }

            services.AddIntegrationEventOutbox<ConsumerTransportDbContext>(ConfigureStorage);
            services.AddIntegrationEventTransportStorage<ConsumerTransportDbContext>();
            services.AddIntegrationEventOutboxStorage<OutboxHealthReadDbContext>();
            services.ConfigureIntegrationEventStorage<OutboxHealthReadDbContext>(options =>
            {
                if (!useDefaultOutbox)
                {
                    options.OutboxSchema = OutboxSchema;
                    options.OutboxTableName = OutboxTable;
                }
                options.ExcludeOutboxFromMigrations = true;
            });
            var commands = new List<string>();
            services.AddDbContext<ConsumerTransportDbContext>(options => options.UseNpgsql(dataSource)
                .EnableServiceProviderCaching(false)
                .LogTo(commands.Add, [RelationalEventId.CommandExecuted]));
            services.AddDbContext<OutboxHealthReadDbContext>(options => options.UseNpgsql(dataSource)
                .EnableServiceProviderCaching(false)
                .LogTo(commands.Add, [RelationalEventId.CommandExecuted]));
            if (initializeLegacy)
            {
                services.AddIntegrationEventOutbox<LegacyMessagingHealthDbContext>(ConfigureStorage);
                services.AddIntegrationEventTransportStorage<LegacyMessagingHealthDbContext>();
                services.UseDefaultIntegrationEventOutbox<ConsumerTransportDbContext>();
                services.AddDbContext<LegacyMessagingHealthDbContext>(options => options.UseNpgsql(dataSource)
                    .EnableServiceProviderCaching(false));
            }

            provider = services.BuildServiceProvider();
            await using var scope = provider.CreateAsyncScope();
            DbContext context = initializeLegacy
                ? scope.ServiceProvider.GetRequiredService<LegacyMessagingHealthDbContext>()
                : scope.ServiceProvider.GetRequiredService<ConsumerTransportDbContext>();
            await context.Database.EnsureCreatedAsync(ct);
            return new PostgreSqlMessagingHealthScenario(database, dataSource, provider, commands);
        }
        catch (Exception failure)
        {
            await PostgreSqlScenarioCleanup.DisposeResources(failure, provider, dataSource, database);
            throw;
        }
    }

    /// <summary>Writes both delivery rows using the storage model that omits workflow correlation columns.</summary>
    internal async Task<string> SeedLegacyDelivery(
        string consumer, DateTimeOffset recordedAt, Guid? extensionWorkflowId, CancellationToken ct)
    {
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<LegacyMessagingHealthDbContext>();
        var integrationEvent = new ComposedMessagingTestIntegrationEvent(Guid.CreateVersion7(), recordedAt);
        var outbox = IntegrationEventOutboxMessageFactory.Create(
            integrationEvent, new ComposedMessagingTestIntegrationEventSerializer(), recordedAt);
        var extensions = extensionWorkflowId is { } id ? $$"""{"workflowid":"{{id:D}}"}""" : null;
        var envelope = TransportEnvelopeFactory.Create(outbox.EventId, extensions);
        var transport = new IntegrationEventTransportMessage(Guid.CreateVersion7(), consumer, envelope, recordedAt);
        context.Set<IntegrationEventOutboxMessage>().Add(outbox);
        context.Set<IntegrationEventTransportMessage>().Add(transport);
        await context.SaveChangesAsync(ct);
        return outbox.EventId;
    }

    /// <summary>Applies the mapped legacy-to-current migration SQL in the disposable database.</summary>
    internal async Task<IReadOnlyList<MigrationOperation>> ExpandWorkflowCorrelation(CancellationToken ct)
    {
        await using var scope = provider.CreateAsyncScope();
        var legacy = scope.ServiceProvider.GetRequiredService<LegacyMessagingHealthDbContext>();
        var current = scope.ServiceProvider.GetRequiredService<ConsumerTransportDbContext>();
        var source = legacy.GetService<IDesignTimeModel>().Model;
        var target = current.GetService<IDesignTimeModel>().Model;
        var changes = current.GetService<IMigrationsModelDiffer>()
            .GetDifferences(source.GetRelationalModel(), target.GetRelationalModel());
        var migration = current.GetService<IMigrationsSqlGenerator>().Generate(changes, target);
        foreach (var command in migration)
        {
            await current.Database.ExecuteSqlRawAsync(command.CommandText, ct);
        }

        return changes;
    }

    /// <summary>Stores a new outbox message after applying any requested test transition.</summary>
    internal async Task<IntegrationEventOutboxMessage> SeedOutbox(DateTimeOffset enqueuedAt, CancellationToken ct,
        Action<IntegrationEventOutboxMessage>? transition = null, Guid? workflowCorrelationId = null)
    {
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ConsumerTransportDbContext>();
        var integrationEvent = new ComposedMessagingTestIntegrationEvent(Guid.CreateVersion7(), enqueuedAt);
        var message = IntegrationEventOutboxMessageFactory.Create(
            integrationEvent, new ComposedMessagingTestIntegrationEventSerializer(), enqueuedAt, workflowCorrelationId);
        transition?.Invoke(message);
        context.Set<IntegrationEventOutboxMessage>().Add(message);
        await context.SaveChangesAsync(ct);
        return message;
    }

    /// <summary>Changes the outbox state in a fresh write scope.</summary>
    internal async Task TransitionOutbox(Guid storageId, Action<IntegrationEventOutboxMessage> transition, CancellationToken ct)
    {
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ConsumerTransportDbContext>();
        var message = await context.Set<IntegrationEventOutboxMessage>().SingleAsync(row => row.Id == storageId, ct);
        transition(message);
        await context.SaveChangesAsync(ct);
    }

    /// <summary>Deletes a retained outbox row in the disposable fixture.</summary>
    internal async Task DeleteOutbox(Guid storageId, CancellationToken ct)
    {
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ConsumerTransportDbContext>();
        await context.Set<IntegrationEventOutboxMessage>().Where(row => row.Id == storageId).ExecuteDeleteAsync(ct);
    }

    /// <summary>Stores a consumer's transport message after applying any requested test transition.</summary>
    internal async Task SeedTransport(string consumer, DateTimeOffset receivedAt, CancellationToken ct,
        Action<IntegrationEventTransportMessage>? transition = null, string? eventId = null, string? extensionAttributesJson = null)
    {
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ConsumerTransportDbContext>();
        var id = Guid.CreateVersion7();
        var envelope = TransportEnvelopeFactory.Create(eventId ?? id.ToString("D"), extensionAttributesJson);
        var message = new IntegrationEventTransportMessage(id, consumer, envelope, receivedAt);
        transition?.Invoke(message);
        context.Set<IntegrationEventTransportMessage>().Add(message);
        await context.SaveChangesAsync(ct);
    }

    /// <summary>Reads the outbox aggregate from a fresh scope.</summary>
    internal async Task<IntegrationEventOutboxHealth> ReadOutbox(DateTimeOffset cutoff, CancellationToken ct)
    {
        commands.Clear();
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<OutboxHealthReadDbContext>();
        return await IntegrationEventMessagingHealth.ReadOutbox(context, cutoff, ct);
    }

    /// <summary>Reads the scoped transport aggregate from a fresh scope.</summary>
    internal async Task<IntegrationEventTransportHealth> ReadTransport(string consumer, DateTimeOffset cutoff, CancellationToken ct)
    {
        commands.Clear();
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ConsumerTransportDbContext>();
        return await IntegrationEventMessagingHealth.ReadTransport(context, consumer, cutoff, ct);
    }

    /// <summary>Inspects outstanding outbox rows through the storage-only context.</summary>
    internal async Task<IntegrationEventDeliveryPage> InspectOutbox(
        IntegrationEventDeliveryFilter filter, int limit, IntegrationEventDeliveryCursor? cursor, CancellationToken ct,
        Guid? correlationId = null)
    {
        commands.Clear();
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<OutboxHealthReadDbContext>();
        return correlationId is { } id
            ? await IntegrationEventDeliveryInspection.ReadMatchingOutbox(context, CorrelatedEvents(context, id), filter, limit, cursor, ct)
            : await IntegrationEventDeliveryInspection.ReadOutbox(context, filter, limit, cursor, ct);
    }

    /// <summary>Stores application-owned correlation membership separately from delivery records.</summary>
    internal async Task SeedCorrelation(Guid correlationId, IReadOnlyList<string> eventIds, CancellationToken ct)
    {
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ConsumerTransportDbContext>();
        await context.Database.ExecuteSqlRawAsync("""
            CREATE TABLE public.delivery_correlation (
                "CorrelationId" uuid NOT NULL,
                "EventId" uuid NOT NULL,
                PRIMARY KEY ("CorrelationId", "EventId")
            )
            """, ct);
        foreach (var eventId in eventIds)
        {
            var uuid = Guid.Parse(eventId);
            await context.Database.ExecuteSqlAsync($"""
                INSERT INTO public.delivery_correlation ("CorrelationId", "EventId")
                VALUES ({correlationId}, {uuid})
                """, ct);
        }
    }

    /// <summary>Looks up retained publication metadata through the storage-only context.</summary>
    internal async Task<IntegrationEventOutboxDelivery?> InspectRetainedOutbox(string eventId, CancellationToken ct)
    {
        commands.Clear();
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<OutboxHealthReadDbContext>();
        return await IntegrationEventDeliveryInspection.ReadRetainedOutbox(context, eventId, ct);
    }

    /// <summary>Reads through a separate login with schema usage and metadata-column SELECT grants.</summary>
    [SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "Schema and table are fixture constants. The role name and password use a fixed ASCII prefix and GUIDs.")]
    internal async Task<(IntegrationEventOutboxDelivery? Delivery, bool HasBroaderAccess)> InspectRetainedOutboxWithMetadataGrants(
        string eventId, CancellationToken ct)
    {
        var role = $"outbox_metadata_{Guid.NewGuid():N}";
        var password = Guid.NewGuid().ToString("N");
        await using var createRole = dataSource.CreateCommand($"CREATE ROLE \"{role}\" LOGIN PASSWORD '{password}';");
        await createRole.ExecuteNonQueryAsync(ct);
        try
        {
            await using var grant = dataSource.CreateCommand($"""
                GRANT USAGE ON SCHEMA {OutboxSchema} TO "{role}";
                GRANT SELECT ("Id", "EventId", "EventType", "EnqueuedAt", "PublishedAt", "PublishAttempts",
                    "LastPublishAttemptAt", "NextPublishAttemptAt")
                    ON {OutboxSchema}.{OutboxTable} TO "{role}";
                """);
            await grant.ExecuteNonQueryAsync(ct);
            await using var restricted = database.CreateDataSource(nameof(OutboxHealthReadDbContext), role, password);
            var options = new DbContextOptionsBuilder<OutboxHealthReadDbContext>().UseNpgsql(restricted)
                .EnableServiceProviderCaching(false).Options;
            var configurations = provider.GetServices<IDbContextConfiguration<OutboxHealthReadDbContext>>();
            await using var reader = new OutboxHealthReadDbContext(options, configurations);
            var delivery = await IntegrationEventDeliveryInspection.ReadRetainedOutbox(reader, eventId, ct);
            await using var probe = restricted.CreateCommand($"""
                SELECT NOT (current_user = @expected_reader)
                    OR has_column_privilege(current_user, '{OutboxSchema}.{OutboxTable}', 'Payload', 'SELECT')
                    OR has_table_privilege(current_user, '{OutboxSchema}.{OutboxTable}', 'INSERT,UPDATE,DELETE')
                """);
            probe.Parameters.AddWithValue("expected_reader", role);
            var hasBroaderAccess = (bool)(await probe.ExecuteScalarAsync(ct))!;
            return (delivery, hasBroaderAccess);
        }
        finally
        {
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await using var dropRole = dataSource.CreateCommand($"""DROP OWNED BY "{role}"; DROP ROLE "{role}";""");
            await dropRole.ExecuteNonQueryAsync(cleanup.Token);
        }
    }

    /// <summary>Inspects outstanding transport rows belonging to one selected consumer.</summary>
    internal async Task<IntegrationEventDeliveryPage> InspectTransport(
        string consumer, IntegrationEventDeliveryFilter filter, int limit, IntegrationEventDeliveryCursor? cursor, CancellationToken ct,
        Guid? correlationId = null)
    {
        commands.Clear();
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ConsumerTransportDbContext>();
        return correlationId is { } id
            ? await IntegrationEventDeliveryInspection.ReadMatchingTransport(context, consumer, CorrelatedEvents(context, id), filter, limit, cursor, ct)
            : await IntegrationEventDeliveryInspection.ReadTransport(context, consumer, filter, limit, cursor, ct);
    }

    private static IQueryable<string> CorrelatedEvents(DbContext context, Guid correlationId)
        => context.Database.SqlQuery<string>($"""
            SELECT "EventId"::text AS "Value"
            FROM public.delivery_correlation
            WHERE "CorrelationId" = {correlationId}
            """);

    /// <inheritdoc />
    public async ValueTask DisposeAsync() => await PostgreSqlScenarioCleanup.DisposeResources(null, provider, dataSource, database);
}
