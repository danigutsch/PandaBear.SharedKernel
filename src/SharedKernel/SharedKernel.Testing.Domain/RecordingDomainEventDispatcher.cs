using PandaBear.Domain;

namespace PandaBear.Testing.Domain;

/// <summary>
/// Dispatcher test double that stores every dispatched event instead of publishing it.
/// </summary>
public sealed class RecordingDomainEventDispatcher : IDomainEventDispatcher
{
    private readonly List<IDomainEvent> dispatched = [];

    /// <summary>
    /// Gets the events dispatched so far, in dispatch order.
    /// </summary>
    public IList<IDomainEvent> Dispatched => dispatched;

    /// <summary>
    /// Forwards the typed event to the non-generic overload so it is recorded.
    /// </summary>
    /// <typeparam name="TDomainEvent">The domain event type.</typeparam>
    /// <param name="domainEvent">The event to record.</param>
    /// <param name="ct">The cancellation token for the dispatch.</param>
    public ValueTask Dispatch<TDomainEvent>(TDomainEvent domainEvent, CancellationToken ct)
        where TDomainEvent : IDomainEvent => Dispatch((IDomainEvent)domainEvent, ct);

    /// <summary>
    /// Appends the event to Dispatched and completes synchronously.
    /// </summary>
    /// <param name="domainEvent">The event to record.</param>
    /// <param name="ct">The cancellation token for the dispatch.</param>
    public ValueTask Dispatch(IDomainEvent domainEvent, CancellationToken ct)
    {
        dispatched.Add(domainEvent);
        return ValueTask.CompletedTask;
    }
}
