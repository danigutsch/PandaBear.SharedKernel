using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel.Idempotency;
using SharedKernel.Idempotency.EntityFrameworkCore;
using SharedKernel.IntegrationTesting;

namespace SharedKernel.EntityFrameworkCore.Tests;

internal sealed class PostgreSqlIdempotencyStoreScenario : IAsyncDisposable
{
    private const string PostgreSqlResourceName = "postgres";
    private const string DatabaseResourceName = "idempotency";

    private readonly AspireTestApplication app;
    private readonly ConcurrentSaveBarrierInterceptor saveBarrier;
    private readonly ServiceProvider serviceProvider;

    private PostgreSqlIdempotencyStoreScenario(
        AspireTestApplication app,
        ConcurrentSaveBarrierInterceptor saveBarrier,
        ServiceProvider serviceProvider)
    {
        this.app = app;
        this.saveBarrier = saveBarrier;
        this.serviceProvider = serviceProvider;
    }

    public static async ValueTask<PostgreSqlIdempotencyStoreScenario> Create(CancellationToken ct)
    {
        var appBuilder = DistributedApplication.CreateBuilder([]);
        var databaseServer = appBuilder.AddPostgres(PostgreSqlResourceName);
        _ = databaseServer.AddDatabase(DatabaseResourceName);

        var app = await AspireTestApplication.Start(appBuilder, [PostgreSqlResourceName], null, ct);
        var connectionString = await app.GetConnectionString(DatabaseResourceName, ct);
        var saveBarrier = new ConcurrentSaveBarrierInterceptor();
        var services = new ServiceCollection();
        services.AddSingleton(saveBarrier);
        services.AddIdempotencyStore<IdempotencyDbContext>();
        services.AddDbContext<IdempotencyDbContext>((serviceProvider, options) =>
        {
            options.UseNpgsql(connectionString);
            options.AddInterceptors(serviceProvider.GetRequiredService<ConcurrentSaveBarrierInterceptor>());
        });
        var serviceProvider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });

        await using var scope = serviceProvider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IdempotencyDbContext>();
        await dbContext.Database.EnsureCreatedAsync(ct);

        return new PostgreSqlIdempotencyStoreScenario(app, saveBarrier, serviceProvider);
    }

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

    public async ValueTask<int> CountEntries(CancellationToken ct)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IdempotencyDbContext>();
        return await dbContext.Set<IdempotencyEntryEntity>().CountAsync(ct);
    }

    public async ValueTask DisposeAsync()
    {
        await serviceProvider.DisposeAsync();
        await app.DisposeAsync();
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
