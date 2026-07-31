using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using SharedKernel.Messaging;
using SharedKernel.Messaging.IntegrationEvents.EntityFrameworkCore;

namespace SharedKernel.EntityFrameworkCore.Tests;

[SuppressMessage(
    "Usage",
    "CA2213:Disposable fields should be disposed",
    Justification = "DisposeAsync passes the context and data source to the independent aggregate cleanup helper.")]
internal sealed class PostgreSqlIntegrationEventTransportScenario : IAsyncDisposable
{
    private const string ConsumerName = "catalog";

    private readonly PostgreSqlTestDatabase database;
    private readonly NpgsqlDataSource dataSource;
    private TransportDbContext? duplicateContext;

    private PostgreSqlIntegrationEventTransportScenario(
        PostgreSqlTestDatabase database,
        NpgsqlDataSource dataSource)
    {
        this.database = database;
        this.dataSource = dataSource;
    }

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

    public async ValueTask SeedMessages(int count, CancellationToken ct)
    {
        await using var dbContext = CreateDbContext();
        await dbContext.Database.EnsureCreatedAsync(ct);
        dbContext.Set<IntegrationEventTransportMessage>().AddRange(
            Enumerable.Range(1, count).Select(index => CreateMessage($"event-{index}")));
        _ = await dbContext.SaveChangesAsync(ct);
    }

    public async ValueTask SeedMessage(string eventId, CancellationToken ct)
    {
        await using var dbContext = CreateDbContext();
        await dbContext.Database.EnsureCreatedAsync(ct);
        dbContext.Set<IntegrationEventTransportMessage>().Add(CreateMessage(eventId));
        _ = await dbContext.SaveChangesAsync(ct);
    }

    public async ValueTask<int> ConsumeWith(
        IEventEnvelopePublisher publisher,
        CancellationToken ct,
        TimeProvider? timeProvider = null,
        ServiceLifetime publisherLifetime = ServiceLifetime.Singleton)
    {
        await using var provider = CreateConsumerProvider(publisher, publisherLifetime, timeProvider);
        var consumer = provider.GetRequiredService<PostgreSqlIntegrationEventTransportConsumer<TransportDbContext>>();

        return await consumer.ConsumePending(1, ct);
    }

    public async ValueTask<int> ConsumeBatchWith(ControlledEventEnvelopePublisher publisher, int batchSize, CancellationToken ct)
    {
        await using var provider = CreateConsumerProvider(publisher, ServiceLifetime.Scoped);
        var consumer = provider.GetRequiredService<PostgreSqlIntegrationEventTransportConsumer<TransportDbContext>>();

        return await consumer.ConsumePending(batchSize, ct);
    }

    public async ValueTask<IntegrationEventTransportMessage> GetMessage(string eventId, CancellationToken ct)
    {
        await using var dbContext = CreateDbContext();
        return await dbContext.Set<IntegrationEventTransportMessage>()
            .SingleAsync(message => message.EventId == eventId, ct);
    }

    public async ValueTask<IntegrationEventTransportMessage[]> ClaimConcurrently(CancellationToken ct)
    {
        await using var firstContext = CreateDbContext();
        await using var secondContext = CreateDbContext();

        var firstClaim = Claim(firstContext, "worker-1", ct).AsTask();
        var secondClaim = Claim(secondContext, "worker-2", ct).AsTask();

        return (await Task.WhenAll(firstClaim, secondClaim)).SelectMany(static messages => messages).ToArray();
    }

    public async ValueTask StageDuplicateDelivery(CancellationToken ct)
    {
        duplicateContext = CreateDbContext();
        await duplicateContext.Database.EnsureCreatedAsync(ct);
        duplicateContext.Set<IntegrationEventTransportMessage>().Add(CreateMessage("duplicate-event"));
        duplicateContext.Set<IntegrationEventTransportMessage>().Add(CreateMessage("duplicate-event"));
    }

    public async Task SaveDuplicateDelivery(CancellationToken ct)
    {
        if (duplicateContext is null)
        {
            throw new InvalidOperationException("Duplicate delivery has not been staged.");
        }

        _ = await duplicateContext.SaveChangesAsync(ct);
    }

    public async ValueTask DisposeAsync()
    {
        await PostgreSqlScenarioCleanup.DisposeResources(null, duplicateContext, dataSource, database);
    }

    private static ValueTask<IntegrationEventTransportMessage[]> Claim(TransportDbContext dbContext, string workerName, CancellationToken ct) =>
        PostgreSqlIntegrationEventTransportClaimSql.ClaimPending(
            dbContext,
            ConsumerName,
            3,
            DateTimeOffset.UtcNow,
            workerName,
            DateTimeOffset.UtcNow.AddMinutes(5),
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

    private ServiceProvider CreateConsumerProvider(
        IEventEnvelopePublisher publisher,
        ServiceLifetime publisherLifetime = ServiceLifetime.Singleton,
        TimeProvider? timeProvider = null)
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

        return services.BuildServiceProvider();
    }

    private static IntegrationEventTransportMessage CreateMessage(string eventId) => new(
        Guid.CreateVersion7(),
        ConsumerName,
        TransportEnvelopeFactory.Create(eventId),
        DateTimeOffset.UtcNow);

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
