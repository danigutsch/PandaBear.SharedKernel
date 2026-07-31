using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using SharedKernel.Messaging.IntegrationEvents;
using SharedKernel.Messaging.IntegrationEvents.EntityFrameworkCore;

namespace SharedKernel.EntityFrameworkCore.Tests;

internal sealed class ContextQualifiedMessagingPostgreSqlScenario : IAsyncDisposable
{
    private const string FirstConsumerName = "first-consumer";
    private const string SecondConsumerName = "second-consumer";

    private readonly PostgreSqlTestDatabase database;
    private readonly NpgsqlDataSource dataSource;
    private readonly ServiceProvider provider;

    private ContextQualifiedMessagingPostgreSqlScenario(
        PostgreSqlTestDatabase database,
        NpgsqlDataSource dataSource,
        ServiceProvider provider,
        string firstCreateScript,
        string secondCreateScript)
    {
        this.database = database;
        this.dataSource = dataSource;
        this.provider = provider;
        FirstCreateScript = firstCreateScript;
        SecondCreateScript = secondCreateScript;
    }

    public string FirstCreateScript { get; }

    public string SecondCreateScript { get; }

    public static async ValueTask<ContextQualifiedMessagingPostgreSqlScenario> Create(
        PostgreSqlFixture fixture,
        CancellationToken ct)
    {
        var database = await fixture.CreateIsolatedDatabase(ct);
        NpgsqlDataSource? dataSource = null;
        ServiceProvider? provider = null;

        try
        {
            dataSource = database.CreateDataSource(nameof(ContextQualifiedMessagingPostgreSqlScenario));
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IIntegrationEventSerializer, ComposedMessagingTestIntegrationEventSerializer>();
            services.AddIntegrationEventOutbox<FirstMessagingDbContext>(options =>
            {
                options.Schema = "first_messaging";
                options.OutboxTableName = "first_outbox";
                options.TransportTableName = "first_transport";
            });
            services.AddIntegrationEventOutbox<SecondMessagingDbContext>(options =>
            {
                options.Schema = "second_messaging";
                options.OutboxTableName = "second_outbox";
                options.TransportTableName = "second_transport";
            });
            services.AddPostgreSqlIntegrationEventTransportProducer<FirstMessagingDbContext>(FirstConsumerName);
            services.AddIntegrationEventOutboxRelay<FirstMessagingDbContext>();
            services.AddIntegrationEventOutboxRelay<SecondMessagingDbContext>();
            services.AddPostgreSqlIntegrationEventTransportConsumer<SecondMessagingDbContext>(SecondConsumerName);
            services.AddDbContext<FirstMessagingDbContext>(options => options.UseNpgsql(dataSource));
            services.AddDbContext<SecondMessagingDbContext>(options => options.UseNpgsql(dataSource));
            provider = services.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true,
            });

            await using var scope = provider.CreateAsyncScope();
            var firstDbContext = scope.ServiceProvider.GetRequiredService<FirstMessagingDbContext>();
            var secondDbContext = scope.ServiceProvider.GetRequiredService<SecondMessagingDbContext>();
            var firstCreateScript = firstDbContext.Database.GenerateCreateScript();
            var secondCreateScript = secondDbContext.Database.GenerateCreateScript();
            await firstDbContext.GetService<IRelationalDatabaseCreator>().CreateTablesAsync(ct);
            await secondDbContext.GetService<IRelationalDatabaseCreator>().CreateTablesAsync(ct);

            return new ContextQualifiedMessagingPostgreSqlScenario(
                database,
                dataSource,
                provider,
                firstCreateScript,
                secondCreateScript);
        }
        catch (Exception creationFailure)
        {
            await PostgreSqlScenarioCleanup.DisposeResources(creationFailure, provider, dataSource, database);
            throw;
        }
    }

    public async Task SaveFirstBusinessRecordWithDuplicateOutboxEvent(CancellationToken ct)
    {
        await using var scope = provider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FirstMessagingDbContext>();
        var outbox = scope.ServiceProvider.GetRequiredKeyedService<IIntegrationEventOutbox>(typeof(FirstMessagingDbContext));
        await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);
        var integrationEvent = new ComposedMessagingTestIntegrationEvent(
            Guid.CreateVersion7(),
            new DateTimeOffset(2026, 7, 24, 12, 0, 0, TimeSpan.Zero));
        dbContext.Set<FirstMessagingBusinessRecord>().Add(new FirstMessagingBusinessRecord(Guid.CreateVersion7()));
        await outbox.Enqueue(integrationEvent, ct);
        await outbox.Enqueue(integrationEvent, ct);

        try
        {
            _ = await dbContext.SaveChangesAsync(ct);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }

        await transaction.RollbackAsync(ct);
        throw new InvalidOperationException("The duplicate outbox event unexpectedly persisted.");
    }

    public async ValueTask CommitSecondBusinessRecordAndOutbox(CancellationToken ct)
    {
        await using var scope = provider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SecondMessagingDbContext>();
        var outbox = scope.ServiceProvider.GetRequiredKeyedService<IIntegrationEventOutbox>(typeof(SecondMessagingDbContext));
        await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);
        dbContext.Set<SecondMessagingBusinessRecord>().Add(new SecondMessagingBusinessRecord(Guid.CreateVersion7()));
        await outbox.Enqueue(new ComposedMessagingTestIntegrationEvent(
            Guid.CreateVersion7(),
            new DateTimeOffset(2026, 7, 24, 12, 1, 0, TimeSpan.Zero)), ct);
        _ = await dbContext.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    public async ValueTask<(int FirstBusiness, int FirstOutbox, int SecondBusiness, int SecondOutbox)> CountRecords(CancellationToken ct)
    {
        await using var scope = provider.CreateAsyncScope();
        var firstDbContext = scope.ServiceProvider.GetRequiredService<FirstMessagingDbContext>();
        var secondDbContext = scope.ServiceProvider.GetRequiredService<SecondMessagingDbContext>();

        return (
            await firstDbContext.Set<FirstMessagingBusinessRecord>().CountAsync(ct),
            await firstDbContext.Set<IntegrationEventOutboxMessage>().CountAsync(ct),
            await secondDbContext.Set<SecondMessagingBusinessRecord>().CountAsync(ct),
            await secondDbContext.Set<IntegrationEventOutboxMessage>().CountAsync(ct));
    }

    public async ValueTask EnqueueSecondOutboxEvent(CancellationToken ct)
    {
        await using var scope = provider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SecondMessagingDbContext>();
        var outbox = scope.ServiceProvider.GetRequiredKeyedService<IIntegrationEventOutbox>(typeof(SecondMessagingDbContext));
        await outbox.Enqueue(new ComposedMessagingTestIntegrationEvent(
            Guid.CreateVersion7(),
            new DateTimeOffset(2026, 7, 24, 12, 2, 0, TimeSpan.Zero)), ct);
        _ = await dbContext.SaveChangesAsync(ct);
    }

    public async ValueTask<(DateTimeOffset? PublishedAt, int TransportCount)> PublishFirstOutboxThroughItsProducer(CancellationToken ct)
    {
        await using (var enqueueScope = provider.CreateAsyncScope())
        {
            var dbContext = enqueueScope.ServiceProvider.GetRequiredService<FirstMessagingDbContext>();
            var outbox = enqueueScope.ServiceProvider.GetRequiredKeyedService<IIntegrationEventOutbox>(typeof(FirstMessagingDbContext));
            await outbox.Enqueue(new ComposedMessagingTestIntegrationEvent(
                Guid.CreateVersion7(),
                new DateTimeOffset(2026, 7, 24, 12, 2, 30, TimeSpan.Zero)), ct);
            _ = await dbContext.SaveChangesAsync(ct);
        }

        var relay = provider.GetRequiredService<EfIntegrationEventOutboxRelay<FirstMessagingDbContext>>();
        _ = await relay.PublishPending(1, ct);

        await using var stateScope = provider.CreateAsyncScope();
        var stateDbContext = stateScope.ServiceProvider.GetRequiredService<FirstMessagingDbContext>();
        var publishedAt = await stateDbContext.Set<IntegrationEventOutboxMessage>()
            .Select(message => message.PublishedAt)
            .SingleAsync(ct);
        var transportCount = await stateDbContext.Set<IntegrationEventTransportMessage>().CountAsync(ct);

        return (publishedAt, transportCount);
    }

    public async ValueTask<int> PublishSecondOutbox(CancellationToken ct)
    {
        var relay = provider.GetRequiredService<EfIntegrationEventOutboxRelay<SecondMessagingDbContext>>();

        return await relay.PublishPending(1, ct);
    }

    public async ValueTask SeedSecondTransportMessage(string eventId, CancellationToken ct)
    {
        await using var scope = provider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SecondMessagingDbContext>();
        dbContext.Set<IntegrationEventTransportMessage>().Add(new IntegrationEventTransportMessage(
            Guid.CreateVersion7(),
            SecondConsumerName,
            TransportEnvelopeFactory.Create(eventId),
            new DateTimeOffset(2026, 7, 24, 12, 3, 0, TimeSpan.Zero)));
        _ = await dbContext.SaveChangesAsync(ct);
    }

    public async ValueTask<int> ConsumeSecondTransport(CancellationToken ct)
    {
        var consumer = provider.GetRequiredService<PostgreSqlIntegrationEventTransportConsumer<SecondMessagingDbContext>>();

        return await consumer.ConsumePending(1, ct);
    }

    public async ValueTask<(DateTimeOffset? PublishedAt, int Attempts, DateTimeOffset? LastAttemptAt, string? ClaimedBy, DateTimeOffset? ClaimedUntil)> GetSecondOutboxState(CancellationToken ct)
    {
        await using var scope = provider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SecondMessagingDbContext>();
        var message = await dbContext.Set<IntegrationEventOutboxMessage>().SingleAsync(ct);

        return (
            message.PublishedAt,
            message.PublishAttempts,
            message.LastPublishAttemptAt,
            message.ClaimedBy,
            message.ClaimedUntil);
    }

    public async ValueTask<(DateTimeOffset? ProcessedAt, int Attempts, DateTimeOffset? LastAttemptAt, string? ClaimedBy, DateTimeOffset? ClaimedUntil)> GetSecondTransportState(
        string eventId,
        CancellationToken ct)
    {
        await using var scope = provider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SecondMessagingDbContext>();
        var message = await dbContext.Set<IntegrationEventTransportMessage>()
            .SingleAsync(candidate => candidate.EventId == eventId, ct);

        return (
            message.ProcessedAt,
            message.ConsumeAttempts,
            message.LastConsumeAttemptAt,
            message.ClaimedBy,
            message.ClaimedUntil);
    }

    public async ValueTask<int> CountFirstTransportMessages(CancellationToken ct)
    {
        await using var scope = provider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FirstMessagingDbContext>();

        return await dbContext.Set<IntegrationEventTransportMessage>().CountAsync(ct);
    }

    public async ValueTask DisposeAsync()
    {
        await PostgreSqlScenarioCleanup.DisposeResources(null, provider, dataSource, database);
    }
}
