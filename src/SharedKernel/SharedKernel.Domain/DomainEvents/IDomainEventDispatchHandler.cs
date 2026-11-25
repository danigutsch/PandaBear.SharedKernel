namespace SharedKernel.Domain;

/// <summary>Handles an untyped domain event as part of a composite dispatch operation.</summary>
public interface IDomainEventDispatchHandler
{
    /// <summary>Handles the provided domain event when its type is supported.</summary>
    /// <param name="domainEvent">The domain event to handle.</param>
    /// <param name="ct">The cancellation token for the operation.</param>
    /// <returns>A task that completes when handling finishes.</returns>
    ValueTask Handle(IDomainEvent domainEvent, CancellationToken ct);
}
