using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace PandaBear.Mediator.EntityFrameworkCore.Tests;

/// <summary>A DI scope with two in-memory contexts and a transaction behavior registered for each context's command, disposed together.</summary>
internal sealed class EfCoreMediatorTestScope : IDisposable
{
    private readonly ServiceProvider provider;
    private readonly IServiceScope scope;

    /// <summary>Builds the service provider with both in-memory contexts and the two command transaction registrations, then opens a scope.</summary>
    public EfCoreMediatorTestScope()
    {
        var services = new ServiceCollection();
        services.AddDbContext<TestDbContext>(options => options.UseInMemoryDatabase(Guid.NewGuid().ToString("N")));
        services.AddDbContext<OtherTestDbContext>(options => options.UseInMemoryDatabase(Guid.NewGuid().ToString("N")));
        services.AddEfCoreCommandTransaction<TestDbContext, TestCommand, int>();
        services.AddEfCoreCommandTransaction<OtherTestDbContext, OtherTestCommand, int>();

        provider = services.BuildServiceProvider();
        scope = provider.CreateScope();
    }

    /// <summary>Gets the scoped TestDbContext.</summary>
    public TestDbContext DbContext => scope.ServiceProvider.GetRequiredService<TestDbContext>();

    /// <summary>Resolves the base DbContext service from the scope, which throws when it was not registered.</summary>
    public DbContext ResolveGlobalDbContext() => scope.ServiceProvider.GetRequiredService<DbContext>();

    /// <summary>Gets the scoped pipeline behavior registered for TestCommand.</summary>
    public IPipelineBehavior<TestCommand, int> CommandBehavior =>
        scope.ServiceProvider.GetRequiredService<IPipelineBehavior<TestCommand, int>>();

    /// <summary>Gets the scoped pipeline behavior registered for OtherTestCommand.</summary>
    public IPipelineBehavior<OtherTestCommand, int> OtherCommandBehavior =>
        scope.ServiceProvider.GetRequiredService<IPipelineBehavior<OtherTestCommand, int>>();

    /// <summary>Attempts to register a command transaction behavior for a query request on a fresh service collection.</summary>
    public static void RegisterQueryTransactionBehavior()
    {
        var services = new ServiceCollection();
        services.AddEfCoreCommandTransaction<TestDbContext, TestQuery, int>();
    }

    /// <summary>Disposes the scope and then the service provider.</summary>
    public void Dispose()
    {
        scope.Dispose();
        provider.Dispose();
    }
}
