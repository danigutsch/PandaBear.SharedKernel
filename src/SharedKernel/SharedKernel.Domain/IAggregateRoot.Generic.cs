namespace SharedKernel.Domain;

/// <summary>
/// Marks a domain aggregate root identified by a stable identifier.
/// </summary>
/// <typeparam name="TId">The aggregate root identifier type.</typeparam>
public interface IAggregateRoot<out TId> : IEntity<TId>, IAggregateRoot
{
    /// <summary>
    /// Gets domain events recorded by this aggregate root.
    /// </summary>
    /// <returns>The recorded domain events.</returns>
    IReadOnlyCollection<IDomainEvent> GetDomainEvents();

    /// <summary>
    /// Clears recorded domain events after successful dispatch.
    /// </summary>
    void ClearDomainEvents();
}
