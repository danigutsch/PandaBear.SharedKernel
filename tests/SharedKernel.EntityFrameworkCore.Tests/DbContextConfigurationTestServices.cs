using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace PandaBear.EntityFrameworkCore.Tests;

/// <summary>
/// Service collection helpers for the DbContext configuration tests.
/// </summary>
internal static class DbContextConfigurationTestServices
{
    /// <summary>
    /// Creates an empty service collection.
    /// </summary>
    public static IServiceCollection Create()
    {
        return new ServiceCollection();
    }

    /// <summary>
    /// Adds TestDbContext on a uniquely named in-memory database, applies the registered option configurations to it and builds the provider.
    /// </summary>
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
