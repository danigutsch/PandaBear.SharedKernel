using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using SharedKernel.Idempotency;
using SharedKernel.Idempotency.EntityFrameworkCore;

namespace SharedKernel.EntityFrameworkCore.Tests;

internal sealed class PostgreSqlIdempotencyStoreScenario : IAsyncDisposable
{
    private readonly PostgreSqlTestDatabase database;
    private readonly NpgsqlDataSource dataSource;
    private readonly ConcurrentSaveBarrierInterceptor saveBarrier;
    private readonly ServiceProvider serviceProvider;

    private PostgreSqlIdempotencyStoreScenario(
        PostgreSqlTestDatabase database,
        NpgsqlDataSource dataSource,
        ConcurrentSaveBarrierInterceptor saveBarrier,
        ServiceProvider serviceProvider)
    {
        this.database = database;
        this.dataSource = dataSource;
        this.saveBarrier = saveBarrier;
        this.serviceProvider = serviceProvider;
    }

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
