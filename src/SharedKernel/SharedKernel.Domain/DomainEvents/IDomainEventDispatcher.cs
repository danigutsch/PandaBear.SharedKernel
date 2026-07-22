namespace SharedKernel.Domain;

/// <summary>
/// Dispatches domain events raised inside one bounded context.
/// </summary>
public interface IDomainEventDispatcher
{
    /// <summary>
    /// Dispatches the specified domain event to matching handlers.
    /// </summary>
    /// <typeparam name="TDomainEvent">The domain event type.</typeparam>
    /// <param name="domainEvent">The domain event instance to dispatch.</param>
    /// <param name="ct">The cancellation token for the operation.</param>
    /// <returns>A task that completes when dispatch finishes.</returns>
    ValueTask Dispatch<TDomainEvent>(TDomainEvent domainEvent, CancellationToken ct)
        where TDomainEvent : IDomainEvent;

    /// <summary>
    /// Dispatches the specified domain event without runtime generic binding.
    /// </summary>
    /// <param name="domainEvent">The domain event instance to dispatch.</param>
    /// <param name="ct">The cancellation token for the operation.</param>
    /// <returns>A task that completes when dispatch finishes.</returns>
    ValueTask Dispatch(IDomainEvent domainEvent, CancellationToken ct);
}
