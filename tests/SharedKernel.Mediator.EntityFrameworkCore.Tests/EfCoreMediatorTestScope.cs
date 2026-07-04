using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace SharedKernel.Mediator.EntityFrameworkCore.Tests;

internal sealed class EfCoreMediatorTestScope : IDisposable
{
    private readonly ServiceProvider provider;
    private readonly IServiceScope scope;

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

    public TestDbContext DbContext => scope.ServiceProvider.GetRequiredService<TestDbContext>();

    public DbContext ResolveGlobalDbContext() => scope.ServiceProvider.GetRequiredService<DbContext>();

    public IPipelineBehavior<TestCommand, int> CommandBehavior =>
        scope.ServiceProvider.GetRequiredService<IPipelineBehavior<TestCommand, int>>();

    public IPipelineBehavior<OtherTestCommand, int> OtherCommandBehavior =>
        scope.ServiceProvider.GetRequiredService<IPipelineBehavior<OtherTestCommand, int>>();

    public static void RegisterQueryTransactionBehavior()
    {
        var services = new ServiceCollection();
        services.AddEfCoreCommandTransaction<TestDbContext, TestQuery, int>();
    }

    public void Dispose()
    {
        scope.Dispose();
        provider.Dispose();
    }
}
