using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace SharedKernel.Mediator.EntityFrameworkCore;

/// <summary>
/// Provides EF Core-backed mediator pipeline registration helpers.
/// </summary>
public static class EfCoreMediatorServiceCollectionExtensions
{
    /// <summary>
    /// Adds a closed EF Core command transaction behavior registration.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <typeparam name="TContext">The DbContext type that owns the transaction boundary.</typeparam>
    /// <typeparam name="TRequest">The command request type.</typeparam>
    /// <typeparam name="TResponse">The command response type.</typeparam>
    /// <returns>The configured service collection.</returns>
    public static IServiceCollection AddEfCoreCommandTransaction<TContext, TRequest, TResponse>(this IServiceCollection services)
        where TContext : DbContext
        where TRequest : IRequest<TResponse>
    {
        ArgumentNullException.ThrowIfNull(services);

        if (!typeof(ICommand).IsAssignableFrom(typeof(TRequest))
            && !typeof(ICommand<TResponse>).IsAssignableFrom(typeof(TRequest)))
        {
            throw new InvalidOperationException("EF Core command transactions can only be registered for command requests.");
        }

        return services.AddScoped<IPipelineBehavior<TRequest, TResponse>>(serviceProvider =>
            new EfCoreCommandTransactionBehavior<TRequest, TResponse>(serviceProvider.GetRequiredService<TContext>()));
    }
}
