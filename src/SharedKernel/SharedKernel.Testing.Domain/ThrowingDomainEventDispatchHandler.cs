using PandaBear.Domain;

namespace PandaBear.Testing.Domain;

/// <summary>Test dispatch handler that always fails with an InvalidOperationException.</summary>
public sealed class ThrowingDomainEventDispatchHandler : IDomainEventDispatchHandler
{
    /// <summary>Returns a faulted ValueTask carrying InvalidOperationException 'The test handler rejected the domain event.', after throwing on a null event or an already cancelled token.</summary>
    /// <param name="domainEvent">The event being dispatched.</param>
    /// <param name="ct">The cancellation token for the dispatch.</param>
    public ValueTask Handle(IDomainEvent domainEvent, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        ct.ThrowIfCancellationRequested();

        return ValueTask.FromException(new InvalidOperationException("The test handler rejected the domain event."));
    }
}
