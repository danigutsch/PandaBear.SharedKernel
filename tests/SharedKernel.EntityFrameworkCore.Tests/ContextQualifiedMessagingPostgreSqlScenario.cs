using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using SharedKernel.Messaging;
using SharedKernel.Messaging.IntegrationEvents;
using SharedKernel.Messaging.IntegrationEvents.EntityFrameworkCore;

namespace SharedKernel.EntityFrameworkCore.Tests;

/// <summary>
/// PostgreSQL-backed host with two messaging contexts (first_messaging and second_messaging schemas) that exposes seeding, publishing, consuming and counting operations for the context-qualified messaging tests.
/// </summary>
internal sealed class ContextQualifiedMessagingPostgreSqlScenario : IAsyncDisposable
{
    private const string FirstConsumerName = "first-consumer";
    private const string SecondConsumerName = "second-consumer";

    private readonly PostgreSqlTestDatabaseLease database;
    private readonly NpgsqlDataSource dataSource;
    private readonly ServiceProvider provider;

    private ContextQualifiedMessagingPostgreSqlScenario(
        PostgreSqlTestDatabaseLease database,
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

    /// <summary>
    /// Create-table SQL generated for FirstMessagingDbContext.
    /// </summary>
    public string FirstCreateScript { get; }

    /// <summary>
    /// Create-table SQL generated for SecondMessagingDbContext.
    /// </summary>
    public string SecondCreateScript { get; }

    /// <summary>
    /// Creates an isolated PostgreSQL database, builds the two-context provider, creates both contexts' tables and returns the scenario, cleaning up everything if creation fails.
    /// </summary>
    /// <param name="fixture">The fixture that leases the isolated database.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <param name="timeProvider">The optional clock for the outbox and relay.</param>
    /// <param name="firstPublisherProbe">The optional probe run before the first producer publishes.</param>
    public static async ValueTask<ContextQualifiedMessagingPostgreSqlScenario> Create(
        PostgreSqlFixture fixture,
        CancellationToken ct,
        TimeProvider? timeProvider = null,
        RecordingEventEnvelopePublisher? firstPublisherProbe = null)
    {
        var database = await fixture.CreateIsolatedDatabase(ct);
        NpgsqlDataSource? dataSource = null;
        ServiceProvider? provider = null;

        try
        {
            dataSource = database.CreateDataSource(nameof(ContextQualifiedMessagingPostgreSqlScenario));
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton(timeProvider ?? TimeProvider.System);
            services.AddSingleton<IIntegrationEventSerializer, ComposedMessagingTestIntegrationEventSerializer>();
            services.AddIntegrationEvents<FirstMessagingDbContext>().WithOutbox(options =>
            {
                options.Schema = "first_messaging";
                options.OutboxTableName = "first_outbox";
                options.TransportTableName = "first_transport";
            }).WithOutboxRelay().WithPostgreSqlAtomicClaims().WithPostgreSqlTransportProducer(FirstConsumerName);
            services.AddIntegrationEvents<SecondMessagingDbContext>().WithOutbox(options =>
            {
                options.Schema = "second_messaging";
                options.OutboxTableName = "second_outbox";
                options.TransportTableName = "second_transport";
            }).WithOutboxRelay().WithPostgreSqlAtomicClaims().WithPostgreSqlTransportConsumer(SecondConsumerName);
            services.AddDbContext<FirstMessagingDbContext>(options => options.UseNpgsql(dataSource));
            services.AddDbContext<SecondMessagingDbContext>(options => options.UseNpgsql(dataSource));
            if (firstPublisherProbe is not null)
            {
                var registration = services.Single(descriptor =>
                    descriptor.ServiceType == typeof(IEventEnvelopePublisher)
                    && Equals(descriptor.ServiceKey, typeof(FirstMessagingDbContext)));
                var factory = registration.KeyedImplementationFactory.ShouldNotBeNull();
                services.Remove(registration);
                services.AddKeyedScoped<IEventEnvelopePublisher>(typeof(FirstMessagingDbContext), (scope, key) =>
                    new ProbedTransportPublisher(firstPublisherProbe, (IEventEnvelopePublisher)factory(scope, key)));
            }

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

    /// <summary>
    /// Enqueues the same integration event twice with a first-context business record in one transaction, rolls the transaction back when the save fails and throws InvalidOperationException if the duplicate unexpectedly persists.
    /// </summary>
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

    /// <summary>
    /// Saves a second-context business record and an outbox event in one transaction and commits it.
    /// </summary>
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

    /// <summary>
    /// Counts business records and outbox messages stored by each of the two contexts.
    /// </summary>
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

    /// <summary>
    /// Enqueues one outbox event through the second context and saves it.
    /// </summary>
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

    /// <summary>
    /// Enqueues one event through the first context and returns its persisted envelope ID.
    /// </summary>
    public async ValueTask<string> EnqueueFirstOutbox(Guid? workflowCorrelationId, CancellationToken ct)
    {
        await using var scope = provider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FirstMessagingDbContext>();
        var outbox = scope.ServiceProvider.GetRequiredKeyedService<IIntegrationEventOutbox>(typeof(FirstMessagingDbContext));
        var integrationEvent = new ComposedMessagingTestIntegrationEvent(
            Guid.CreateVersion7(), new DateTimeOffset(2026, 7, 24, 12, 2, 30, TimeSpan.Zero));
        if (workflowCorrelationId is { } id)
        {
            await outbox.Enqueue(integrationEvent, id, ct);
        }
        else
        {
            await outbox.Enqueue(integrationEvent, ct);
        }

        _ = await dbContext.SaveChangesAsync(ct);
        return integrationEvent.EventId.ToString("D");
    }

    /// <summary>Runs the first outbox's atomic claim and relay for one message.</summary>
    public ValueTask<int> PublishFirstOutbox(CancellationToken ct)
    {
        var relay = provider.GetRequiredService<EfIntegrationEventOutboxRelay<FirstMessagingDbContext>>();
        return relay.PublishPending(1, ct);
    }

    /// <summary>Reads pending outbox and transport pages for the same workflow in a new scope.</summary>
    public async Task<(IntegrationEventDeliveryPage Outbox, IntegrationEventDeliveryPage Transport)> InspectFirstDeliveryQueues(
        Guid workflowCorrelationId, CancellationToken ct)
    {
        await using var scope = provider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FirstMessagingDbContext>();
        var filter = new IntegrationEventDeliveryFilter(null, null, null) { WorkflowCorrelationId = workflowCorrelationId };
        var outbox = await IntegrationEventDeliveryInspection.ReadOutbox(dbContext, filter, 10, null, ct);
        var transport = await IntegrationEventDeliveryInspection.ReadTransport(dbContext, FirstConsumerName, filter, 10, null, ct);
        return (outbox, transport);
    }

    /// <summary>Reads the retained outbox view and checks the same row's persisted workflow value and error bound separately.</summary>
    public async Task<(IntegrationEventOutboxDelivery Retained, Guid? StoredWorkflowId, int? StoredErrorLength)> InspectFirstRetainedOutbox(
        string eventId, CancellationToken ct)
    {
        await using var scope = provider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FirstMessagingDbContext>();
        var retained = await IntegrationEventDeliveryInspection.ReadRetainedOutbox(dbContext, eventId, ct);
        var stored = await dbContext.Set<IntegrationEventOutboxMessage>()
            .Where(message => message.EventId == eventId)
            .Select(message => new { message.WorkflowCorrelationId, message.LastPublishError })
            .SingleAsync(ct);
        return (retained.ShouldNotBeNull(), stored.WorkflowCorrelationId, stored.LastPublishError?.Length);
    }

    /// <summary>
    /// Enqueues an event through the first context, publishes it with the relay and returns the message's PublishedAt and the number of transport messages written.
    /// </summary>
    public async ValueTask<(DateTimeOffset? PublishedAt, int TransportCount)> PublishFirstOutboxThroughItsProducer(CancellationToken ct)
    {
        _ = await EnqueueFirstOutbox(workflowCorrelationId: null, ct);
        _ = await PublishFirstOutbox(ct);
        await using var stateScope = provider.CreateAsyncScope();
        var stateDbContext = stateScope.ServiceProvider.GetRequiredService<FirstMessagingDbContext>();
        var publishedAt = await stateDbContext.Set<IntegrationEventOutboxMessage>()
            .Select(message => message.PublishedAt)
            .SingleAsync(ct);
        var transportCount = await stateDbContext.Set<IntegrationEventTransportMessage>().CountAsync(ct);

        return (publishedAt, transportCount);
    }

    /// <summary>
    /// Runs the second context's outbox relay for one message and returns the number published.
    /// </summary>
    public async ValueTask<int> PublishSecondOutbox(CancellationToken ct)
    {
        var relay = provider.GetRequiredService<EfIntegrationEventOutboxRelay<SecondMessagingDbContext>>();

        return await relay.PublishPending(1, ct);
    }

    /// <summary>
    /// Stores a pending second-context transport message for the given event id.
    /// </summary>
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

    /// <summary>
    /// Runs the second context's PostgreSQL transport consumer for one message and returns the number consumed.
    /// </summary>
    public async ValueTask<int> ConsumeSecondTransport(CancellationToken ct)
    {
        var consumer = provider.GetRequiredService<PostgreSqlIntegrationEventTransportConsumer<SecondMessagingDbContext>>();

        return await consumer.ConsumePending(1, ct);
    }

    /// <summary>
    /// Reads the single second-context outbox message's published time, attempts, last attempt time and claim owner and expiry.
    /// </summary>
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

    /// <summary>
    /// Reads the processed time, attempts, last attempt time and claim owner and expiry of the second-context transport message with the given event id.
    /// </summary>
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

    /// <summary>
    /// Counts the transport messages stored by the first context.
    /// </summary>
    public async ValueTask<int> CountFirstTransportMessages(CancellationToken ct)
    {
        await using var scope = provider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FirstMessagingDbContext>();

        return await dbContext.Set<IntegrationEventTransportMessage>().CountAsync(ct);
    }

    /// <summary>
    /// Releases the provider, data source and isolated database.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        await PostgreSqlScenarioCleanup.DisposeResources(null, provider, dataSource, database);
    }
}
