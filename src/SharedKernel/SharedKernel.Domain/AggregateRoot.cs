namespace SharedKernel.Domain;

/// <summary>
/// Base class for aggregate roots that record domain events.
/// </summary>
/// <typeparam name="TId">The aggregate root identifier type.</typeparam>
public abstract class AggregateRoot<TId> : Entity<TId>, IAggregateRoot
{
    private readonly List<IDomainEvent> domainEvents = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="AggregateRoot{TId}"/> class.
    /// </summary>
    protected AggregateRoot()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AggregateRoot{TId}"/> class with the specified identifier.
    /// </summary>
    /// <param name="id">The aggregate root identifier.</param>
    protected AggregateRoot(TId id)
        : base(id)
    {
    }

    /// <summary>
    /// Gets domain events recorded by this aggregate root.
    /// </summary>
    /// <returns>The recorded domain events.</returns>
    public IReadOnlyCollection<IDomainEvent> GetDomainEvents() => domainEvents.AsReadOnly();

    /// <summary>
    /// Clears recorded domain events after successful dispatch.
    /// </summary>
    public void ClearDomainEvents() => domainEvents.Clear();

    /// <summary>
    /// Records a domain event raised by aggregate behavior.
    /// </summary>
    /// <param name="domainEvent">The domain event to record.</param>
    protected void AddDomainEvent(IDomainEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        domainEvents.Add(domainEvent);
    }
}
