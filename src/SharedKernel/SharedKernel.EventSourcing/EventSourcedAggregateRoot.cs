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

        foreach (var @event in events)
        {
            ArgumentNullException.ThrowIfNull(@event);

            ApplyEvent(@event);
            Version++;
        }
    }

    /// <summary>
    /// Applies and tracks a new event.
    /// </summary>
    /// <param name="event">The event to apply.</param>
    protected void AddEvent(object @event)
    {
        ArgumentNullException.ThrowIfNull(@event);

        ApplyEvent(@event);
        Version++;
        uncommittedEvents.Add(@event);
    }

    /// <summary>
    /// Applies an event to aggregate state.
    /// </summary>
    /// <param name="event">The event to apply.</param>
    protected abstract void ApplyEvent(object @event);
}
