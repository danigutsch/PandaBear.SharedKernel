using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace SharedKernel.Mediator.EntityFrameworkCore;

/// <summary>
/// Provides EF Core-backed mediator pipeline registration helpers.
/// </summary>
public static class EfCoreMediatorServiceCollectionExtensions
{
    /// <summary>
    /// Adds the EF Core command transaction behavior for a module DbContext.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <typeparam name="TContext">The DbContext type that owns the transaction boundary.</typeparam>
    /// <returns>The configured service collection.</returns>
    public static IServiceCollection AddEfCoreCommandTransactions<TContext>(this IServiceCollection services)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<DbContext>(sp => sp.GetRequiredService<TContext>());
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(EfCoreCommandTransactionBehavior<,>));

        return services;
    }
}
