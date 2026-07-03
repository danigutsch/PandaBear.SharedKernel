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
        services.AddEfCoreCommandTransactions<TestDbContext>();

        provider = services.BuildServiceProvider();
        scope = provider.CreateScope();
    }

    public TestDbContext DbContext => scope.ServiceProvider.GetRequiredService<TestDbContext>();

    public DbContext TransactionBoundary => scope.ServiceProvider.GetRequiredService<DbContext>();

    public IPipelineBehavior<TestCommand, int> CommandBehavior =>
        scope.ServiceProvider.GetRequiredService<IPipelineBehavior<TestCommand, int>>();

    public void Dispose()
    {
        scope.Dispose();
        provider.Dispose();
    }
}
