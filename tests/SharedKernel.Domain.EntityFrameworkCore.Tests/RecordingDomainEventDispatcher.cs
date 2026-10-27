
namespace PandaBear.Domain.EntityFrameworkCore.Tests;

/// <summary>
/// Dispatcher test double that stores every dispatched event instead of publishing it.
/// </summary>
internal sealed class RecordingDomainEventDispatcher : IDomainEventDispatcher
{
    /// <summary>
    /// Events dispatched so far, in dispatch order.
    /// </summary>
    public List<IDomainEvent> Dispatched { get; } = [];

    /// <summary>
    /// Forwards the typed event to the non-generic overload so it is recorded.
    /// </summary>
    public ValueTask Dispatch<TDomainEvent>(TDomainEvent domainEvent, CancellationToken ct)
        where TDomainEvent : IDomainEvent => Dispatch((IDomainEvent)domainEvent, ct);

    /// <summary>
    /// Appends the event to Dispatched and completes synchronously.
    /// </summary>
    public ValueTask Dispatch(IDomainEvent domainEvent, CancellationToken ct)
    {
        Dispatched.Add(domainEvent);
        return ValueTask.CompletedTask;
    }
}
