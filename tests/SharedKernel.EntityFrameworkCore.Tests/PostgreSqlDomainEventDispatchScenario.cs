using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using PandaBear.Domain;
using PandaBear.Domain.EntityFrameworkCore;

namespace PandaBear.EntityFrameworkCore.Tests;

internal sealed class PostgreSqlDomainEventDispatchScenario : IAsyncDisposable
{
    private readonly PostgreSqlTestDatabaseLease database;
    private readonly NpgsqlDataSource dataSource;
    private readonly ServiceProvider serviceProvider;

    private PostgreSqlDomainEventDispatchScenario(
        PostgreSqlTestDatabaseLease database,
        NpgsqlDataSource dataSource,
        ServiceProvider serviceProvider,
        PostgreSqlDomainEventDispatcher dispatcher)
    {
        this.database = database;
        this.dataSource = dataSource;
        this.serviceProvider = serviceProvider;
        Dispatcher = dispatcher;
    }

    public PostgreSqlDomainEventDispatcher Dispatcher
    {
        get;
    }

    public static async ValueTask<PostgreSqlDomainEventDispatchScenario> Create(
        PostgreSqlFixture fixture,
        bool failDispatch,
        CancellationToken ct)
    {
        var database = await fixture.CreateIsolatedDatabase(ct);
        var dataSource = database.CreateDataSource(nameof(PostgreSqlDomainEventDispatchScenario));
        var dispatcher = new PostgreSqlDomainEventDispatcher(failDispatch);
        var services = new ServiceCollection();
        services.AddSingleton<IDomainEventDispatcher>(dispatcher);
        services.AddDomainEventDispatch<PostgreSqlDomainEventDbContext>();
        services.AddDbContext<PostgreSqlDomainEventDbContext>(options =>
        {
            options.UseNpgsql(dataSource);
            services.ApplyDbContextOptionConfigurations<PostgreSqlDomainEventDbContext>(options, enableDevelopmentDiagnostics: false);
        });
        ServiceProvider? serviceProvider = null;
        try
        {
            serviceProvider = services.BuildServiceProvider();
            await using var scope = serviceProvider.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<PostgreSqlDomainEventDbContext>().Database.EnsureCreatedAsync(ct);

            return new PostgreSqlDomainEventDispatchScenario(database, dataSource, serviceProvider, dispatcher);
        }
        catch (Exception creationFailure)
        {
            await PostgreSqlScenarioCleanup.DisposeResources(creationFailure, serviceProvider, dataSource, database);
            throw;
        }
    }

    public async ValueTask SavePlacedOrder(int id, CancellationToken ct)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PostgreSqlDomainEventDbContext>();
        var order = new PostgreSqlDomainEventOrder { Id = id };
        order.Place();
        context.Orders.Add(order);
        _ = await context.SaveChangesAsync(ct);
    }

    public async ValueTask<int> CountOrders(CancellationToken ct)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<PostgreSqlDomainEventDbContext>().Orders.CountAsync(ct);
    }

    public async ValueTask DisposeAsync()
    {
        await PostgreSqlScenarioCleanup.DisposeResources(null, serviceProvider, dataSource, database);
    }
}
