namespace SharedKernel.Domain;

/// <summary>
/// Marks a domain aggregate root identified by a stable identifier.
/// </summary>
/// <typeparam name="TId">The aggregate root identifier type.</typeparam>
public interface IAggregateRoot<out TId> : IEntity<TId>, IAggregateRoot;
