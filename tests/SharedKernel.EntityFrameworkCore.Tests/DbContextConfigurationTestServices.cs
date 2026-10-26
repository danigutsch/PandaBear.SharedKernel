using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace PandaBear.EntityFrameworkCore.Tests;

internal static class DbContextConfigurationTestServices
{
    public static IServiceCollection Create()
    {
        return new ServiceCollection();
    }

    public static ServiceProvider BuildInMemoryProvider(IServiceCollection services)
    {
        var databaseName = Guid.NewGuid().ToString();
        _ = services.AddDbContext<TestDbContext>(options =>
        {
            _ = options.UseInMemoryDatabase(databaseName);
            services.ApplyDbContextOptionConfigurations<TestDbContext>(options, enableDevelopmentDiagnostics: false);
        });
        return services.BuildServiceProvider();
    }
}
