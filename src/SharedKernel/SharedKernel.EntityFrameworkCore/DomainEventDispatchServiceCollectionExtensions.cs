using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace SharedKernel.EntityFrameworkCore;

/// <summary>
/// Provides service registration helpers for EF Core domain-event dispatching.
/// </summary>
public static class DomainEventDispatchServiceCollectionExtensions
{
    /// <summary>
    /// Adds domain-event dispatch interception for a DbContext.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <typeparam name="TContext">The DbContext type.</typeparam>
    /// <returns>The configured service collection.</returns>
    public static IServiceCollection AddDomainEventDispatch<TContext>(this IServiceCollection services)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<DispatchDomainEventsSaveChangesInterceptor>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IDbContextConfiguration<TContext>, DomainEventDispatchDbContextConfiguration<TContext>>());

        return services;
    }
}
