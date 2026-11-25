namespace SharedKernel.Domain;

/// <summary>
/// Marks an entity as an aggregate root that records domain events.
/// </summary>
public interface IAggregateRoot
{
    /// <summary>
    /// Gets domain events recorded by this aggregate root.
    /// </summary>
    /// <returns>The recorded domain events.</returns>
    IReadOnlyCollection<IDomainEvent> GetDomainEvents();

    /// <summary>
    /// Clears recorded domain events after successful persistence.
    /// </summary>
    void ClearDomainEvents();
}
