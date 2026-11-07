using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PandaBear.Results;

namespace PandaBear.Mediator;

/// <summary>
/// Provides registration helpers for <see cref="ConcurrencyConflictBehavior{TRequest, TResponse}"/>.
/// </summary>
public static class ConcurrencyConflictServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="ConcurrencyConflictBehavior{TRequest, TResponse}"/> as the closed pipeline behavior of one request.
    /// Calling it again for the same request and response adds nothing.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <typeparam name="TRequest">The request type.</typeparam>
    /// <typeparam name="TResponse">The <see cref="Result"/> or <see cref="Result{T}"/> the request produces.</typeparam>
    /// <returns>The configured service collection.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is <see langword="null"/>.</exception>
    public static IServiceCollection AddConcurrencyConflictHandling<TRequest, TResponse>(this IServiceCollection services)
        where TRequest : IRequest<TResponse>
        where TResponse : struct, IResultLike<TResponse>
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddEnumerable(ServiceDescriptor.Scoped<IPipelineBehavior<TRequest, TResponse>, ConcurrencyConflictBehavior<TRequest, TResponse>>());
        return services;
    }
}
