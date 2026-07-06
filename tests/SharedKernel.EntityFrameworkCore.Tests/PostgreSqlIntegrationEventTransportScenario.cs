using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SharedKernel.IntegrationTesting;
using SharedKernel.Messaging;
using SharedKernel.Messaging.IntegrationEvents.EntityFrameworkCore;

namespace SharedKernel.EntityFrameworkCore.Tests;

internal sealed class PostgreSqlIntegrationEventTransportScenario : IAsyncDisposable
{
    private const string PostgreSqlResourceName = "postgres";
    private const string DatabaseResourceName = "transport";
    private const string ConsumerName = "catalog";

    private readonly AspireTestApplication app;
    private readonly string connectionString;
    private TransportDbContext? duplicateContext;

    private PostgreSqlIntegrationEventTransportScenario(AspireTestApplication app, string connectionString)
    {
        this.app = app;
        this.connectionString = connectionString;
    }

    public static async ValueTask<PostgreSqlIntegrationEventTransportScenario> Create(CancellationToken ct)
    {
        var appBuilder = DistributedApplication.CreateBuilder([]);
        var databaseServer = appBuilder.AddPostgres(PostgreSqlResourceName);
        _ = databaseServer.AddDatabase(DatabaseResourceName);

        var app = await AspireTestApplication.Start(appBuilder, [PostgreSqlResourceName], null, ct);
        var connectionString = await app.GetConnectionString(DatabaseResourceName, ct);

        return new PostgreSqlIntegrationEventTransportScenario(app, connectionString);
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

    public async ValueTask<int> ConsumeWith(RecordingEventEnvelopePublisher publisher, CancellationToken ct)
    {
        await using var provider = CreateConsumerProvider(publisher);
        var consumer = provider.GetRequiredService<PostgreSqlIntegrationEventTransportConsumer<TransportDbContext>>();

        return await consumer.ConsumePending(1, ct);
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
        if (duplicateContext is not null)
        {
            await duplicateContext.DisposeAsync();
        }

        await app.DisposeAsync();
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
            .UseNpgsql(connectionString)
            .Options;

        return new TransportDbContext(
            options,
            [new IntegrationEventTransportDbContextConfiguration<TransportDbContext>()]);
    }

    private ServiceProvider CreateConsumerProvider(RecordingEventEnvelopePublisher publisher)
    {
        var services = new ServiceCollection();
        services.AddDbContext<TransportDbContext>(options => options.UseNpgsql(connectionString));
        services.AddSingleton<IDbContextConfiguration<TransportDbContext>, IntegrationEventTransportDbContextConfiguration<TransportDbContext>>();
        services.AddSingleton<IEventEnvelopePublisher>(publisher);
        services.AddSingleton(TimeProvider.System);
        services.AddOptions<IntegrationEventOutboxRelayOptions>();
        services.AddSingleton(sp => new PostgreSqlIntegrationEventTransportConsumer<TransportDbContext>(
            sp.GetRequiredService<IServiceScopeFactory>(),
            sp.GetRequiredService<TimeProvider>(),
            sp.GetRequiredService<IOptions<IntegrationEventOutboxRelayOptions>>(),
            ConsumerName));

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
