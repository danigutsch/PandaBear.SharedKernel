using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel.EntityFrameworkCore;

namespace SharedKernel.Domain.EntityFrameworkCore;

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

        if (!services.Any(static descriptor =>
                descriptor.ServiceType == typeof(IDbContextConfiguration<TContext>)
                && descriptor.ImplementationInstance is DomainEventDispatchDbContextConfiguration<TContext>))
        {
            services.AddDbContextConfiguration(new DomainEventDispatchDbContextConfiguration<TContext>());
        }

        return services;
    }
}
