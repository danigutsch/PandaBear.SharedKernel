namespace SharedKernel.EventSourcing;

/// <summary>
/// Base type for event-sourced aggregate roots.
/// </summary>
/// <typeparam name="TId">The aggregate identifier type.</typeparam>
public abstract class EventSourcedAggregateRoot<TId>
{
    private readonly List<object> uncommittedEvents = [];

    /// <summary>
    /// Gets the aggregate identifier.
    /// </summary>
    public abstract TId Id { get; }

    /// <summary>
    /// Gets the current aggregate version after replayed and pending events.
    /// </summary>
    public long Version { get; private set; }

    /// <summary>
    /// Gets events that have been applied but not yet persisted.
    /// </summary>
    /// <returns>The uncommitted events.</returns>
    public IReadOnlyCollection<object> GetUncommittedEvents() => uncommittedEvents.AsReadOnly();

    /// <summary>
    /// Clears tracked uncommitted events after persistence succeeds.
    /// </summary>
    public void ClearUncommittedEvents() => uncommittedEvents.Clear();

    /// <summary>
    /// Replays persisted events without marking them as uncommitted.
    /// </summary>
    /// <param name="events">The persisted events to replay.</param>
    public void Replay(IEnumerable<object> events)
    {
        ArgumentNullException.ThrowIfNull(events);

        foreach (var domainEvent in events)
        {
            ArgumentNullException.ThrowIfNull(domainEvent);

            ApplyEvent(domainEvent);
            Version++;
        }
    }

    /// <summary>
    /// Applies and tracks a new event.
    /// </summary>
    /// <param name="domainEvent">The event to apply.</param>
    protected void AddEvent(object domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        ApplyEvent(domainEvent);
        Version++;
        uncommittedEvents.Add(domainEvent);
    }

    /// <summary>
    /// Applies an event to aggregate state.
    /// </summary>
    /// <param name="domainEvent">The event to apply.</param>
    protected abstract void ApplyEvent(object domainEvent);
}
