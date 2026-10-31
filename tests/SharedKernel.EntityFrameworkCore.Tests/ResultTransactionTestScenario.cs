using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using PandaBear.Mediator.EntityFrameworkCore;
using PandaBear.Results;

namespace PandaBear.EntityFrameworkCore.Tests;

/// <summary>
/// Builds a service provider with a retrying PostgreSQL context, recording hooks and logger for the result transaction behavior.
/// </summary>
internal sealed class ResultTransactionTestScenario : IAsyncDisposable
{
    private readonly ServiceProvider provider;

    /// <summary>Creates the scenario on the given data source.</summary>
    /// <param name="dataSource">The isolated database data source.</param>
    public ResultTransactionTestScenario(NpgsqlDataSource dataSource)
    {
        ArgumentNullException.ThrowIfNull(dataSource);

        var services = new ServiceCollection();
        services.AddDbContext<TestDbContext>(options => options.UseNpgsql(
            dataSource,
            npgsql => npgsql.EnableRetryOnFailure(3, TimeSpan.FromMilliseconds(50), null)));
        services.AddScoped<DbContext>(static sp => sp.GetRequiredService<TestDbContext>());
        services.AddSingleton(Hooks);
        services.AddSingleton<IAfterCommit<ResultTransactionTestCommand, Result>>(Hooks);
        services.AddSingleton<IAfterRollback<ResultTransactionTestCommand, Result>>(Hooks);
        provider = services.BuildServiceProvider();
    }

    /// <summary>Gets the shared recording hooks.</summary>
    public ResultTransactionTestHooks Hooks { get; } = new();

    /// <summary>Gets the logger handed to every behavior.</summary>
    public RecordingLogger Logger { get; } = new();

    /// <summary>Creates a behavior with its own scope and context, as a request would get.</summary>
    /// <returns>The behavior and the context it uses.</returns>
    public (ResultTransactionTestBehavior Behavior, TestDbContext Context, IServiceScope Scope) CreateBehavior()
    {
        var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TestDbContext>();
        var behavior = new ResultTransactionTestBehavior(context, provider.GetRequiredService<IServiceScopeFactory>(), Logger);
        return (behavior, context, scope);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => provider.DisposeAsync();
}
