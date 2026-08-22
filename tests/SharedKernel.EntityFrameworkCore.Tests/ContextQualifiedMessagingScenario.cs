using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using SharedKernel.Idempotency;
using SharedKernel.Idempotency.EntityFrameworkCore;
using SharedKernel.Messaging;
using SharedKernel.Messaging.IntegrationEvents;
using SharedKernel.Messaging.IntegrationEvents.EntityFrameworkCore;

namespace SharedKernel.EntityFrameworkCore.Tests;

internal sealed class ContextQualifiedMessagingScenario : IAsyncDisposable
{
    private const string FirstConsumerName = "first-consumer";
    private const string SecondConsumerName = "second-consumer";
    private readonly RecordingEventEnvelopePublisher applicationPublisher;
    private readonly ServiceProvider provider;

    private ContextQualifiedMessagingScenario(
        ServiceProvider provider,
        RecordingEventEnvelopePublisher applicationPublisher)
    {
        this.provider = provider;
        this.applicationPublisher = applicationPublisher;
    }

    public static ContextQualifiedMessagingScenario Create()
    {
        return Create(firstPublisher: null, addTransportProducers: true);
    }

    public static ContextQualifiedMessagingScenario CreateWithFirstPublisher(IEventEnvelopePublisher firstPublisher)
    {
        ArgumentNullException.ThrowIfNull(firstPublisher);

        return Create(firstPublisher, addTransportProducers: true);
    }

    public static ContextQualifiedMessagingScenario CreateWithoutTransportProducers()
    {
        return Create(firstPublisher: null, addTransportProducers: false);
    }

    private static ContextQualifiedMessagingScenario Create(
        IEventEnvelopePublisher? firstPublisher,
        bool addTransportProducers)
    {
        var services = new ServiceCollection();
        var firstDatabaseName = $"first-{Guid.CreateVersion7():N}";
        var secondDatabaseName = $"second-{Guid.CreateVersion7():N}";
        var applicationPublisher = new RecordingEventEnvelopePublisher();
        services.AddLogging();
        services.AddSingleton<IEventEnvelopePublisher>(applicationPublisher);
        services.AddSingleton<IIntegrationEventSerializer, ComposedMessagingTestIntegrationEventSerializer>();
        services.AddKeyedSingleton<IIntegrationEventSerializer>(
            typeof(FirstMessagingDbContext),
            new ContextQualifiedMessagingTestIntegrationEventSerializer(
                ContextQualifiedMessagingTestIntegrationEventSerializer.FirstPayload));
        services.AddKeyedSingleton<IIntegrationEventSerializer>(
            typeof(SecondMessagingDbContext),
            new ContextQualifiedMessagingTestIntegrationEventSerializer(
                ContextQualifiedMessagingTestIntegrationEventSerializer.SecondPayload));
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
        services.AddIntegrationEventInbox<FirstMessagingDbContext>(options =>
        {
            options.Schema = "first_messaging";
            options.TableName = "first_inbox";
        });
        services.AddIntegrationEventInbox<SecondMessagingDbContext>(options =>
        {
            options.Schema = "second_messaging";
            options.TableName = "second_inbox";
        });
        if (addTransportProducers)
        {
            services.AddPostgreSqlIntegrationEventTransportProducer<FirstMessagingDbContext>(FirstConsumerName);
            services.AddPostgreSqlIntegrationEventTransportProducer<SecondMessagingDbContext>(SecondConsumerName);
        }
        services.AddPostgreSqlIntegrationEventTransportConsumer<FirstMessagingDbContext>(FirstConsumerName, options => options.BatchSize = 3);
        services.AddPostgreSqlIntegrationEventTransportConsumer<SecondMessagingDbContext>(SecondConsumerName, options => options.BatchSize = 4);
        if (firstPublisher is not null)
        {
            services.AddKeyedScoped(typeof(FirstMessagingDbContext), (_, _) => firstPublisher);
        }
        services.AddIntegrationEventOutboxRelay<FirstMessagingDbContext>(options => options.BatchSize = 1);
        services.AddIntegrationEventOutboxRelay<SecondMessagingDbContext>(options => options.BatchSize = 2);
        services.AddDbContext<FirstMessagingDbContext>(options =>
            options.UseInMemoryDatabase(firstDatabaseName));
        services.AddDbContext<SecondMessagingDbContext>(options =>
            options.UseInMemoryDatabase(secondDatabaseName));

        var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });

        return new ContextQualifiedMessagingScenario(provider, applicationPublisher);
    }

    public static async ValueTask<(int ApplicationPublished, int TransportCount)> PublishWithApplicationRegisteredAfterProducer(
        CancellationToken ct)
    {
        var services = new ServiceCollection();
        var applicationPublisher = new RecordingEventEnvelopePublisher();
        services.AddPostgreSqlIntegrationEventTransportProducer<DefaultMessagingDbContext>("producer-first");
        services.TryAddScoped<IEventEnvelopePublisher>(_ => applicationPublisher);
        services.AddDbContext<DefaultMessagingDbContext>(options =>
            options.UseInMemoryDatabase($"producer-first-{Guid.CreateVersion7():N}"));
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });
        await using var scope = provider.CreateAsyncScope();
        var publisher = scope.ServiceProvider.GetRequiredService<IEventEnvelopePublisher>();
        await publisher.Publish(TransportEnvelopeFactory.Create("application-event"), ct);
        var dbContext = scope.ServiceProvider.GetRequiredService<DefaultMessagingDbContext>();
        _ = await dbContext.SaveChangesAsync(ct);

        return (
            applicationPublisher.Published.Count,
            await dbContext.Set<IntegrationEventTransportMessage>().CountAsync(ct));
    }

    public int GetApplicationPublishedCount() => applicationPublisher.Published.Count;

    public ValueTask<(Guid[] First, Guid[] Second)> EnqueueInEachContext(CancellationToken ct)
    {
        return EnqueueInEachContext(1, ct);
    }

    public async ValueTask<(Guid[] First, Guid[] Second)> EnqueueInEachContext(int countPerContext, CancellationToken ct)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(countPerContext);

        await using var scope = provider.CreateAsyncScope();
        var firstOutbox = scope.ServiceProvider.GetRequiredKeyedService<IIntegrationEventOutbox>(typeof(FirstMessagingDbContext));
        var secondOutbox = scope.ServiceProvider.GetRequiredKeyedService<IIntegrationEventOutbox>(typeof(SecondMessagingDbContext));
        var occurredAt = new DateTimeOffset(2026, 7, 24, 12, 0, 0, TimeSpan.Zero);
        var firstEventIds = new Guid[countPerContext];
        var secondEventIds = new Guid[countPerContext];

        for (var index = 0; index < countPerContext; index++)
        {
            firstEventIds[index] = Guid.CreateVersion7();
            secondEventIds[index] = Guid.CreateVersion7();
            await firstOutbox.Enqueue(new ComposedMessagingTestIntegrationEvent(firstEventIds[index], occurredAt), ct);
            await secondOutbox.Enqueue(new ComposedMessagingTestIntegrationEvent(secondEventIds[index], occurredAt), ct);
        }

        var firstDbContext = scope.ServiceProvider.GetRequiredService<FirstMessagingDbContext>();
        var secondDbContext = scope.ServiceProvider.GetRequiredService<SecondMessagingDbContext>();
        _ = await firstDbContext.SaveChangesAsync(ct);
        _ = await secondDbContext.SaveChangesAsync(ct);

        return (firstEventIds, secondEventIds);
    }

    public async ValueTask<(int First, int Second)> CountOutboxMessages(CancellationToken ct)
    {
        await using var scope = provider.CreateAsyncScope();
        var firstDbContext = scope.ServiceProvider.GetRequiredService<FirstMessagingDbContext>();
        var secondDbContext = scope.ServiceProvider.GetRequiredService<SecondMessagingDbContext>();
        var firstCount = await firstDbContext.Set<IntegrationEventOutboxMessage>().CountAsync(ct);
        var secondCount = await secondDbContext.Set<IntegrationEventOutboxMessage>().CountAsync(ct);

        return (firstCount, secondCount);
    }

    public async ValueTask<(string? First, string? Second)> GetSingleOutboxPayloads(CancellationToken ct)
    {
        await using var scope = provider.CreateAsyncScope();
        var firstDbContext = scope.ServiceProvider.GetRequiredService<FirstMessagingDbContext>();
        var secondDbContext = scope.ServiceProvider.GetRequiredService<SecondMessagingDbContext>();
        var firstPayload = await firstDbContext.Set<IntegrationEventOutboxMessage>()
            .Select(message => message.Payload)
            .SingleAsync(ct);
        var secondPayload = await secondDbContext.Set<IntegrationEventOutboxMessage>()
            .Select(message => message.Payload)
            .SingleAsync(ct);

        return (firstPayload, secondPayload);
    }

    public async ValueTask<string?> GetSecondOutboxPayload(CancellationToken ct)
    {
        await using var scope = provider.CreateAsyncScope();
        var secondDbContext = scope.ServiceProvider.GetRequiredService<SecondMessagingDbContext>();

        return await secondDbContext.Set<IntegrationEventOutboxMessage>()
            .Select(message => message.Payload)
            .SingleAsync(ct);
    }

    public async ValueTask EnqueueDomainEventInSecondContext(CancellationToken ct)
    {
        await using var scope = provider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SecondMessagingDbContext>();
        var outbox = scope.ServiceProvider.GetRequiredService<IDomainEventIntegrationEventOutbox>();
        var integrationEvent = new ComposedMessagingTestIntegrationEvent(
            Guid.CreateVersion7(),
            new DateTimeOffset(2026, 7, 24, 12, 0, 0, TimeSpan.Zero));

        using (CurrentSaveChangesDbContext.Enter(dbContext))
        {
            await outbox.Enqueue(integrationEvent, ct);
        }

        _ = await dbContext.SaveChangesAsync(ct);
    }

    public async ValueTask StartIdempotentOperationsInEachContext(CancellationToken ct)
    {
        await using var scope = provider.CreateAsyncScope();
        var firstStore = scope.ServiceProvider.GetRequiredKeyedService<IIdempotencyStore>(typeof(FirstMessagingDbContext));
        var secondStore = scope.ServiceProvider.GetRequiredKeyedService<IIdempotencyStore>(typeof(SecondMessagingDbContext));
        var startedAt = new DateTimeOffset(2026, 7, 24, 12, 0, 0, TimeSpan.Zero);

        _ = await firstStore.TryStart(CreateOperation("first"), startedAt, null, ct);
        _ = await secondStore.TryStart(CreateOperation("second"), startedAt, null, ct);
    }

    public async ValueTask<(int First, int Second)> CountIdempotencyEntries(CancellationToken ct)
    {
        await using var scope = provider.CreateAsyncScope();
        var firstDbContext = scope.ServiceProvider.GetRequiredService<FirstMessagingDbContext>();
        var secondDbContext = scope.ServiceProvider.GetRequiredService<SecondMessagingDbContext>();
        var firstCount = await firstDbContext.Set<IdempotencyEntryEntity>().CountAsync(ct);
        var secondCount = await secondDbContext.Set<IdempotencyEntryEntity>().CountAsync(ct);

        return (firstCount, secondCount);
    }

    public async ValueTask PublishTransportMessagesInEachContext(CancellationToken ct)
    {
        await using var scope = provider.CreateAsyncScope();
        var firstPublisher = scope.ServiceProvider.GetRequiredKeyedService<IEventEnvelopePublisher>(typeof(FirstMessagingDbContext));
        var secondPublisher = scope.ServiceProvider.GetRequiredKeyedService<IEventEnvelopePublisher>(typeof(SecondMessagingDbContext));
        await firstPublisher.Publish(TransportEnvelopeFactory.Create("first-event"), ct);
        await secondPublisher.Publish(TransportEnvelopeFactory.Create("second-event"), ct);

        var firstDbContext = scope.ServiceProvider.GetRequiredService<FirstMessagingDbContext>();
        var secondDbContext = scope.ServiceProvider.GetRequiredService<SecondMessagingDbContext>();
        _ = await firstDbContext.SaveChangesAsync(ct);
        _ = await secondDbContext.SaveChangesAsync(ct);
    }

    public async ValueTask<(string First, string Second)> GetTransportDestinations(CancellationToken ct)
    {
        await using var scope = provider.CreateAsyncScope();
        var firstDbContext = scope.ServiceProvider.GetRequiredService<FirstMessagingDbContext>();
        var secondDbContext = scope.ServiceProvider.GetRequiredService<SecondMessagingDbContext>();
        var firstDestination = await firstDbContext.Set<IntegrationEventTransportMessage>()
            .Select(message => message.ConsumerName)
            .SingleAsync(ct);
        var secondDestination = await secondDbContext.Set<IntegrationEventTransportMessage>()
            .Select(message => message.ConsumerName)
            .SingleAsync(ct);

        return (firstDestination, secondDestination);
    }

    public async ValueTask<(string First, string Second)> GetTransportEventIds(CancellationToken ct)
    {
        await using var scope = provider.CreateAsyncScope();
        var firstDbContext = scope.ServiceProvider.GetRequiredService<FirstMessagingDbContext>();
        var secondDbContext = scope.ServiceProvider.GetRequiredService<SecondMessagingDbContext>();
        var firstEventId = await firstDbContext.Set<IntegrationEventTransportMessage>()
            .Select(message => message.EventId)
            .SingleAsync(ct);
        var secondEventId = await secondDbContext.Set<IntegrationEventTransportMessage>()
            .Select(message => message.EventId)
            .SingleAsync(ct);

        return (firstEventId, secondEventId);
    }

    public async ValueTask<(int ApplicationPublished, int FirstTransport, int SecondTransport)> PublishThroughApplicationPublisher(CancellationToken ct)
    {
        await using var scope = provider.CreateAsyncScope();
        var publisher = scope.ServiceProvider.GetRequiredService<IEventEnvelopePublisher>();
        await publisher.Publish(TransportEnvelopeFactory.Create("application-event"), ct);
        var firstDbContext = scope.ServiceProvider.GetRequiredService<FirstMessagingDbContext>();
        var secondDbContext = scope.ServiceProvider.GetRequiredService<SecondMessagingDbContext>();

        return (
            applicationPublisher.Published.Count,
            await firstDbContext.Set<IntegrationEventTransportMessage>().CountAsync(ct),
            await secondDbContext.Set<IntegrationEventTransportMessage>().CountAsync(ct));
    }

    public (int First, int Second) GetTransportConsumerBatchSizes()
    {
        _ = provider.GetRequiredService<PostgreSqlIntegrationEventTransportConsumer<FirstMessagingDbContext>>();
        _ = provider.GetRequiredService<PostgreSqlIntegrationEventTransportConsumer<SecondMessagingDbContext>>();
        var options = provider.GetRequiredService<IOptionsMonitor<IntegrationEventOutboxRelayOptions>>();

        return (
            options.Get(IntegrationEventOptionsNames.Consumer<FirstMessagingDbContext>()).BatchSize,
            options.Get(IntegrationEventOptionsNames.Consumer<SecondMessagingDbContext>()).BatchSize);
    }

    public async ValueTask<(Guid First, Guid Second)> PublishEachOutboxThroughItsRelay(CancellationToken ct)
    {
        var eventIds = await EnqueueInEachContext(ct);
        var firstRelay = provider.GetRequiredService<EfIntegrationEventOutboxRelay<FirstMessagingDbContext>>();
        var secondRelay = provider.GetRequiredService<EfIntegrationEventOutboxRelay<SecondMessagingDbContext>>();
        _ = await firstRelay.PublishPending(1, ct);
        _ = await secondRelay.PublishPending(1, ct);

        return (eventIds.First[0], eventIds.Second[0]);
    }

    public async ValueTask EnqueueInFirstContext(CancellationToken ct)
    {
        await using var scope = provider.CreateAsyncScope();
        var outbox = scope.ServiceProvider.GetRequiredKeyedService<IIntegrationEventOutbox>(typeof(FirstMessagingDbContext));
        await outbox.Enqueue(new ComposedMessagingTestIntegrationEvent(
            Guid.CreateVersion7(),
            new DateTimeOffset(2026, 7, 24, 12, 0, 0, TimeSpan.Zero)), ct);
        var dbContext = scope.ServiceProvider.GetRequiredService<FirstMessagingDbContext>();
        _ = await dbContext.SaveChangesAsync(ct);
    }

    public async ValueTask<int> PublishFirstRelay(CancellationToken ct)
    {
        var relay = provider.GetRequiredService<EfIntegrationEventOutboxRelay<FirstMessagingDbContext>>();

        return await relay.PublishPending(1, ct);
    }

    public async ValueTask<(DateTimeOffset? PublishedAt, int Attempts, string? LastError, string? ClaimedBy, DateTimeOffset? ClaimedUntil)> GetFirstOutboxState(CancellationToken ct)
    {
        await using var scope = provider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FirstMessagingDbContext>();
        var message = await dbContext.Set<IntegrationEventOutboxMessage>().SingleAsync(ct);

        return (message.PublishedAt, message.PublishAttempts, message.LastPublishError, message.ClaimedBy, message.ClaimedUntil);
    }

    public async ValueTask<(int First, int Second)> PublishUsingContextRelayOptions(CancellationToken ct)
    {
        await EnqueueInEachContext(2, ct);
        var firstRelay = provider.GetRequiredService<EfIntegrationEventOutboxRelay<FirstMessagingDbContext>>();
        var secondRelay = provider.GetRequiredService<EfIntegrationEventOutboxRelay<SecondMessagingDbContext>>();
        _ = await firstRelay.PublishPending(ct);
        _ = await secondRelay.PublishPending(ct);

        await using var scope = provider.CreateAsyncScope();
        var firstDbContext = scope.ServiceProvider.GetRequiredService<FirstMessagingDbContext>();
        var secondDbContext = scope.ServiceProvider.GetRequiredService<SecondMessagingDbContext>();
        var firstCount = await firstDbContext.Set<IntegrationEventTransportMessage>().CountAsync(ct);
        var secondCount = await secondDbContext.Set<IntegrationEventTransportMessage>().CountAsync(ct);

        return (firstCount, secondCount);
    }

    public async ValueTask<(string OutboxSchema, string OutboxTable, string TransportSchema, string TransportTable, string InboxSchema, string InboxTable)> GetFirstStorageMappings()
    {
        await using var scope = provider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FirstMessagingDbContext>();

        return GetStorageMappings(dbContext);
    }

    public async ValueTask<(string OutboxSchema, string OutboxTable, string TransportSchema, string TransportTable, string InboxSchema, string InboxTable)> GetSecondStorageMappings()
    {
        await using var scope = provider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SecondMessagingDbContext>();

        return GetStorageMappings(dbContext);
    }

    public static async ValueTask<(string OutboxSchema, string OutboxTable, string TransportSchema, string TransportTable, string InboxSchema, string InboxTable)> GetDefaultStorageMappings()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IIntegrationEventSerializer, ComposedMessagingTestIntegrationEventSerializer>();
        services.AddIntegrationEventOutbox<DefaultMessagingDbContext>();
        services.AddIntegrationEventInbox<DefaultMessagingDbContext>();
        services.AddPostgreSqlIntegrationEventTransportProducer<DefaultMessagingDbContext>("default-consumer");
        services.AddDbContext<DefaultMessagingDbContext>(options =>
            options.UseInMemoryDatabase($"default-{Guid.CreateVersion7():N}"));
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });
        await using var scope = provider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DefaultMessagingDbContext>();

        return GetStorageMappings(dbContext);
    }

    public static async ValueTask<(string OutboxSchema, string OutboxTable, string TransportSchema, string TransportTable, string OutboxSql, string TransportSql)> GetSplitSchemaStorage()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IIntegrationEventSerializer, ComposedMessagingTestIntegrationEventSerializer>();
        services.AddIntegrationEventOutbox<SplitSchemaMessagingDbContext>(options =>
        {
            options.Schema = "fallback_messaging";
            options.OutboxSchema = "branding";
            options.OutboxTableName = "outbox_messages";
            options.TransportSchema = "messaging";
            options.TransportTableName = "transport_messages";
            options.ExcludeTransportFromMigrations = true;
        });
        services.AddPostgreSqlIntegrationEventTransportProducer<SplitSchemaMessagingDbContext>("split-schema-consumer");
        services.AddDbContext<SplitSchemaMessagingDbContext>(options =>
            options.UseInMemoryDatabase($"split-schema-{Guid.CreateVersion7():N}"));
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });
        await using var scope = provider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SplitSchemaMessagingDbContext>();
        var outbox = dbContext.Model.FindEntityType(typeof(IntegrationEventOutboxMessage)).ShouldNotBeNull();
        var transport = dbContext.GetService<IDesignTimeModel>().Model
            .FindEntityType(typeof(IntegrationEventTransportMessage))
            .ShouldNotBeNull();

        return (
            outbox.GetSchema().ShouldNotBeNull(),
            outbox.GetTableName().ShouldNotBeNull(),
            transport.GetSchema().ShouldNotBeNull(),
            transport.GetTableName().ShouldNotBeNull(),
            PostgreSqlIntegrationEventOutboxClaimStrategy<SplitSchemaMessagingDbContext>.CreateClaimSql(outbox),
            PostgreSqlIntegrationEventTransportClaimSql.CreateClaimSql(transport));
    }

    public static async ValueTask<bool> IsSplitSchemaTransportExcludedFromMigrations()
    {
        var services = new ServiceCollection();
        services.ConfigureIntegrationEventStorage<SplitSchemaMessagingDbContext>(options =>
        {
            options.TransportSchema = "messaging";
            options.TransportTableName = "transport_messages";
            options.ExcludeTransportFromMigrations = true;
        });
        services.AddIntegrationEventTransportStorage<SplitSchemaMessagingDbContext>();
        services.AddDbContext<SplitSchemaMessagingDbContext>(options =>
            options.UseInMemoryDatabase($"split-schema-ownership-{Guid.CreateVersion7():N}"));
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });
        await using var scope = provider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SplitSchemaMessagingDbContext>();
        var transport = dbContext.GetService<IDesignTimeModel>().Model
            .FindEntityType(typeof(IntegrationEventTransportMessage))
            .ShouldNotBeNull();

        return transport.IsTableExcludedFromMigrations();
    }

    public async ValueTask<(string FirstOutbox, string FirstTransport, string SecondOutbox, string SecondTransport)> GetClaimSql()
    {
        await using var scope = provider.CreateAsyncScope();
        var firstDbContext = scope.ServiceProvider.GetRequiredService<FirstMessagingDbContext>();
        var secondDbContext = scope.ServiceProvider.GetRequiredService<SecondMessagingDbContext>();

        return (
            PostgreSqlIntegrationEventOutboxClaimStrategy<FirstMessagingDbContext>.CreateClaimSql(
                firstDbContext.Model.FindEntityType(typeof(IntegrationEventOutboxMessage)).ShouldNotBeNull()),
            PostgreSqlIntegrationEventTransportClaimSql.CreateClaimSql(
                firstDbContext.Model.FindEntityType(typeof(IntegrationEventTransportMessage)).ShouldNotBeNull()),
            PostgreSqlIntegrationEventOutboxClaimStrategy<SecondMessagingDbContext>.CreateClaimSql(
                secondDbContext.Model.FindEntityType(typeof(IntegrationEventOutboxMessage)).ShouldNotBeNull()),
            PostgreSqlIntegrationEventTransportClaimSql.CreateClaimSql(
                secondDbContext.Model.FindEntityType(typeof(IntegrationEventTransportMessage)).ShouldNotBeNull()));
    }

    private static (string OutboxSchema, string OutboxTable, string TransportSchema, string TransportTable, string InboxSchema, string InboxTable) GetStorageMappings(DbContext dbContext)
    {
        var outbox = dbContext.Model.FindEntityType(typeof(IntegrationEventOutboxMessage)).ShouldNotBeNull();
        var transport = dbContext.Model.FindEntityType(typeof(IntegrationEventTransportMessage)).ShouldNotBeNull();
        var inbox = dbContext.Model.FindEntityType(typeof(IdempotencyEntryEntity)).ShouldNotBeNull();

        return (
            outbox.GetSchema().ShouldNotBeNull(),
            outbox.GetTableName().ShouldNotBeNull(),
            transport.GetSchema().ShouldNotBeNull(),
            transport.GetTableName().ShouldNotBeNull(),
            inbox.GetSchema().ShouldNotBeNull(),
            inbox.GetTableName().ShouldNotBeNull());
    }

    private static IdempotencyOperation CreateOperation(string key) => new(
        IdempotencyScope.From("sharedkernel.tests.composed"),
        IdempotencyKey.From(key));

    public async ValueTask DisposeAsync()
    {
        await provider.DisposeAsync();
    }
}
