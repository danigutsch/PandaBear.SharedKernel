using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using PandaBear.Messaging;
using PandaBear.Messaging.IntegrationEvents.EntityFrameworkCore;

namespace PandaBear.EntityFrameworkCore.Tests;

/// <summary>
/// PostgreSQL-backed host that seeds, claims and consumes transport messages for the consumer "catalog" in an isolated database.
/// </summary>
[SuppressMessage(
    "Usage",
    "CA2213:Disposable fields should be disposed",
    Justification = "DisposeAsync passes the context and data source to the independent aggregate cleanup helper.")]
internal sealed class PostgreSqlIntegrationEventTransportScenario : IAsyncDisposable
{
    private const string ConsumerName = "catalog";

    private readonly PostgreSqlTestDatabaseLease database;
    private readonly NpgsqlDataSource dataSource;
    private TransportDbContext? duplicateContext;

    private PostgreSqlIntegrationEventTransportScenario(
        PostgreSqlTestDatabaseLease database,
        NpgsqlDataSource dataSource)
    {
        this.database = database;
        this.dataSource = dataSource;
    }

    /// <summary>
    /// Leases an isolated PostgreSQL database and opens a data source on it, releasing the database if creation fails.
    /// </summary>
    public static async ValueTask<PostgreSqlIntegrationEventTransportScenario> Create(
        PostgreSqlFixture fixture,
        CancellationToken ct)
    {
        var database = await fixture.CreateIsolatedDatabase(ct);
        try
        {
            var dataSource = database.CreateDataSource(nameof(PostgreSqlIntegrationEventTransportScenario));
            return new PostgreSqlIntegrationEventTransportScenario(database, dataSource);
        }
        catch (Exception creationFailure)
        {
            await PostgreSqlScenarioCleanup.DisposeResources(creationFailure, database);
            throw;
        }
    }

    /// <summary>
    /// Creates the schema and inserts the given number of transport messages with ids event-1 to event-N.
    /// </summary>
    public async ValueTask SeedMessages(int count, CancellationToken ct)
    {
        await using var dbContext = CreateDbContext();
        await dbContext.Database.EnsureCreatedAsync(ct);
        dbContext.Set<IntegrationEventTransportMessage>().AddRange(
            Enumerable.Range(1, count).Select(index => CreateMessage($"event-{index}")));
        _ = await dbContext.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Creates the schema and inserts one transport message with the given event id.
    /// </summary>
    public async ValueTask SeedMessage(string eventId, CancellationToken ct)
    {
        await using var dbContext = CreateDbContext();
        await dbContext.Database.EnsureCreatedAsync(ct);
        dbContext.Set<IntegrationEventTransportMessage>().Add(CreateMessage(eventId));
        _ = await dbContext.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Runs the PostgreSQL transport consumer once for one message with the given publisher, clock, publisher lifetime and attempt limit, and returns the number consumed.
    /// </summary>
    public async ValueTask<int> ConsumeWith(
        IEventEnvelopePublisher publisher,
        CancellationToken ct,
        TimeProvider? timeProvider = null,
        ServiceLifetime publisherLifetime = ServiceLifetime.Singleton,
        int? maxConsumeAttempts = null)
    {
        await using var provider = CreateConsumerProvider(publisher, publisherLifetime, timeProvider, maxConsumeAttempts);
        var consumer = provider.GetRequiredService<PostgreSqlIntegrationEventTransportConsumer<TransportDbContext>>();

        return await consumer.ConsumePending(1, ct);
    }

    /// <summary>
    /// Runs the transport consumer with a scoped controlled publisher and the given batch size, and returns the number consumed.
    /// </summary>
    public async ValueTask<int> ConsumeBatchWith(ControlledEventEnvelopePublisher publisher, int batchSize, CancellationToken ct)
    {
        await using var provider = CreateConsumerProvider(publisher, ServiceLifetime.Scoped);
        var consumer = provider.GetRequiredService<PostgreSqlIntegrationEventTransportConsumer<TransportDbContext>>();

        return await consumer.ConsumePending(batchSize, ct);
    }

    /// <summary>
    /// Loads the stored transport message with the given event id.
    /// </summary>
    public async ValueTask<IntegrationEventTransportMessage> GetMessage(string eventId, CancellationToken ct)
    {
        await using var dbContext = CreateDbContext();
        return await dbContext.Set<IntegrationEventTransportMessage>()
            .SingleAsync(message => message.EventId == eventId, ct);
    }

    /// <summary>
    /// Claims messages from two contexts at the same time as workers "worker-1" and "worker-2" and returns everything either claimed.
    /// </summary>
    public async ValueTask<IntegrationEventTransportMessage[]> ClaimConcurrently(CancellationToken ct)
    {
        await using var firstContext = CreateDbContext();
        await using var secondContext = CreateDbContext();

        var firstClaim = Claim(firstContext, "worker-1", ct).AsTask();
        var secondClaim = Claim(secondContext, "worker-2", ct).AsTask();

        return (await Task.WhenAll(firstClaim, secondClaim)).SelectMany(static messages => messages).ToArray();
    }

    /// <summary>
    /// Creates the schema and stages two transport messages with the same event id in a context without saving them.
    /// </summary>
    public async ValueTask StageDuplicateDelivery(CancellationToken ct)
    {
        duplicateContext = CreateDbContext();
        await duplicateContext.Database.EnsureCreatedAsync(ct);
        duplicateContext.Set<IntegrationEventTransportMessage>().Add(CreateMessage("duplicate-event"));
        duplicateContext.Set<IntegrationEventTransportMessage>().Add(CreateMessage("duplicate-event"));
    }

    /// <summary>
    /// Saves the staged duplicate deliveries, throwing InvalidOperationException when none were staged.
    /// </summary>
    public async Task SaveDuplicateDelivery(CancellationToken ct)
    {
        if (duplicateContext is null)
        {
            throw new InvalidOperationException("Duplicate delivery has not been staged.");
        }

        _ = await duplicateContext.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Disposes the staged context, data source and isolated database.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        await PostgreSqlScenarioCleanup.DisposeResources(null, duplicateContext, dataSource, database);
    }

    private static ValueTask<IntegrationEventTransportMessage[]> Claim(TransportDbContext dbContext, string workerName, CancellationToken ct) =>
        PostgreSqlIntegrationEventTransportClaimSql.ClaimPending(
            dbContext,
            ConsumerName,
            3,
            TimeProvider.System.GetUtcNow(),
            workerName,
            TimeProvider.System.GetUtcNow().AddMinutes(5),
            ct);

    private TransportDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<TransportDbContext>()
            .UseNpgsql(dataSource)
            .Options;

        return new TransportDbContext(
            options,
            [new IntegrationEventTransportDbContextConfiguration<TransportDbContext>()]);
    }

    /// <summary>
    /// Checks that exactly one message is poisoned for the consumer, then releases the one with the given event id and requires that to succeed.
    /// </summary>
    public async ValueTask ReplayPoisoned(string eventId, DateTimeOffset now, CancellationToken ct)
    {
        await using var dbContext = CreateDbContext();
        (await IntegrationEventTransportPoison.CountPoisoned(dbContext, ConsumerName, ct)).ShouldBe(1);
        (await IntegrationEventTransportPoison.ReleasePoisoned(dbContext, ConsumerName, eventId, now, ct)).ShouldBeTrue();
    }

    private ServiceProvider CreateConsumerProvider(
        IEventEnvelopePublisher publisher,
        ServiceLifetime publisherLifetime = ServiceLifetime.Singleton,
        TimeProvider? timeProvider = null,
        int? maxConsumeAttempts = null)
    {
        var services = new ServiceCollection();
        services.AddDbContext<TransportDbContext>(options => options.UseNpgsql(dataSource));
        services.AddSingleton<IDbContextConfiguration<TransportDbContext>, IntegrationEventTransportDbContextConfiguration<TransportDbContext>>();
        _ = publisherLifetime switch
        {
            ServiceLifetime.Singleton => services.AddSingleton(publisher),
            ServiceLifetime.Scoped => services.AddScoped(_ => publisher),
            _ => throw new ArgumentOutOfRangeException(nameof(publisherLifetime), publisherLifetime, "Unsupported test publisher lifetime.")
        };
        services.AddSingleton(timeProvider ?? TimeProvider.System);
        services.AddPostgreSqlIntegrationEventTransportConsumer<TransportDbContext>(ConsumerName);
        services.Configure<IntegrationEventOutboxRelayOptions>(
            IntegrationEventOptionsNames.Consumer<TransportDbContext>(),
            options => options.MaxConsumeAttempts = maxConsumeAttempts);

        return services.BuildServiceProvider();
    }

    private static IntegrationEventTransportMessage CreateMessage(string eventId) => new(
        Guid.CreateVersion7(),
        ConsumerName,
        TransportEnvelopeFactory.Create(eventId),
        TimeProvider.System.GetUtcNow());

    private sealed class TransportDbContext(
        DbContextOptions<TransportDbContext> options,
        IEnumerable<IDbContextConfiguration<TransportDbContext>> configurations) : DbContext(options)
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
