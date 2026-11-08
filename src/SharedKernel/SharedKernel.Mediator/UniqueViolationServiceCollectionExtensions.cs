using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PandaBear.Results;

namespace PandaBear.Mediator;

/// <summary>
/// Provides registration helpers for <see cref="UniqueViolationBehavior{TRequest, TResponse}"/> and its <see cref="UniqueRuleMap"/>.
/// </summary>
public static class UniqueViolationServiceCollectionExtensions
{
    /// <summary>
    /// Registers the <see cref="UniqueRuleMap"/> singleton and applies <paramref name="configure"/> to it. Calling it more than once
    /// merges the rules into the same map; mapping a rule that is already mapped throws <see cref="ArgumentException"/>.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configure">Adds the rule mappings.</param>
    /// <returns>The configured service collection.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> or <paramref name="configure"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when a rule is mapped twice.</exception>
    public static IServiceCollection AddUniqueViolationHandling(this IServiceCollection services, Action<UniqueRuleMap> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var map = services
            .Where(static d => d.ServiceType == typeof(UniqueRuleMap))
            .Select(static d => d.ImplementationInstance)
            .OfType<UniqueRuleMap>()
            .FirstOrDefault();

        if (map is null)
        {
            map = new UniqueRuleMap();
            services.AddSingleton(map);
        }

        configure(map);
        return services;
    }

    /// <summary>
    /// Registers <see cref="UniqueViolationBehavior{TRequest, TResponse}"/> as the closed pipeline behavior of one request.
    /// Calling it again for the same request and response adds nothing. The behavior uses the <see cref="UniqueRuleMap"/>
    /// registered by <see cref="AddUniqueViolationHandling(IServiceCollection, Action{UniqueRuleMap})"/>, or answers every violation with a conflict when none is registered.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <typeparam name="TRequest">The request type.</typeparam>
    /// <typeparam name="TResponse">The <see cref="Result"/> or <see cref="Result{T}"/> the request produces.</typeparam>
    /// <returns>The configured service collection.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is <see langword="null"/>.</exception>
    public static IServiceCollection AddUniqueViolationHandling<TRequest, TResponse>(this IServiceCollection services)
        where TRequest : IRequest<TResponse>
        where TResponse : struct, IResultLike<TResponse>
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddEnumerable(ServiceDescriptor.Scoped<IPipelineBehavior<TRequest, TResponse>, UniqueViolationBehavior<TRequest, TResponse>>());
        return services;
    }
}
