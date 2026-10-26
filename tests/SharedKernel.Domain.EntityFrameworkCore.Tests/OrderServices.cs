using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PandaBear.EntityFrameworkCore;

namespace PandaBear.Domain.EntityFrameworkCore.Tests;

internal static class OrderServices
{
    public static ServiceProvider BuildProvider(RecordingDomainEventDispatcher dispatcher)
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton<IDomainEventDispatcher>(dispatcher);
        _ = services.AddDomainEventDispatch<OrderDbContext>();
        var databaseName = Guid.NewGuid().ToString();
        _ = services.AddDbContext<OrderDbContext>(options =>
        {
            _ = options.UseInMemoryDatabase(databaseName);
            services.ApplyDbContextOptionConfigurations<OrderDbContext>(options, enableDevelopmentDiagnostics: false);
        });
        return services.BuildServiceProvider();
    }
}
