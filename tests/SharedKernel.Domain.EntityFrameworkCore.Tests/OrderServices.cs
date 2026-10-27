using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using PandaBear.EntityFrameworkCore;

namespace PandaBear.Domain.EntityFrameworkCore.Tests;

/// <summary>
/// Builds service providers for the order domain-event dispatch tests.
/// </summary>
internal static class OrderServices
{
    /// <summary>
    /// Builds a provider with the given dispatcher, domain-event dispatch and an in-memory OrderDbContext, optionally adding an interceptor that runs after dispatch.
    /// </summary>
    public static ServiceProvider BuildProvider(RecordingDomainEventDispatcher dispatcher, SaveChangesInterceptor? afterDispatch = null)
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton<IDomainEventDispatcher>(dispatcher);
        _ = services.AddDomainEventDispatch<OrderDbContext>();
        var databaseName = Guid.NewGuid().ToString();
        _ = services.AddDbContext<OrderDbContext>(options =>
        {
            _ = options.UseInMemoryDatabase(databaseName);
            services.ApplyDbContextOptionConfigurations<OrderDbContext>(options, enableDevelopmentDiagnostics: false);
            if (afterDispatch is not null)
            {
                _ = options.AddInterceptors(afterDispatch);
            }
        });
        return services.BuildServiceProvider();
    }
}
