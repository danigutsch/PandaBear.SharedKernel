using PandaBear.Domain;

namespace PandaBear.Testing.Domain;

/// <summary>Test dispatch handler that appends its own name to a shared call list each time it handles an event.</summary>
/// <param name="name">The name appended to the call list on every handled event.</param>
/// <param name="calls">The shared list that records which handlers ran and in what order.</param>
public sealed class CapturingDomainEventDispatchHandler(string name, IList<string> calls) : IDomainEventDispatchHandler
{
    /// <summary>Throws when the event is null or the token is already cancelled, otherwise adds the handler name to the call list.</summary>
    /// <param name="domainEvent">The event being dispatched.</param>
    /// <param name="ct">The cancellation token for the dispatch.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="domainEvent"/> is <see langword="null"/>.</exception>
    public ValueTask Handle(IDomainEvent domainEvent, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        ct.ThrowIfCancellationRequested();

        calls.Add(name);
        return ValueTask.CompletedTask;
    }
}
