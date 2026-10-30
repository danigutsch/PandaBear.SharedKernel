using PandaBear.Domain;

namespace PandaBear.Testing.Domain;

/// <summary>Test dispatch handler that remembers the cancellation token passed to its last Handle call.</summary>
public sealed class CancellationCapturingDomainEventDispatchHandler : IDomainEventDispatchHandler
{
    /// <summary>The cancellation token received by the most recent Handle call.</summary>
    public CancellationToken CapturedToken
    {
        get; private set;
    }

    /// <summary>Stores the supplied token in CapturedToken and completes synchronously, throwing when the event is null.</summary>
    /// <param name="domainEvent">The event being dispatched.</param>
    /// <param name="ct">The cancellation token for the dispatch.</param>
    public ValueTask Handle(IDomainEvent domainEvent, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        CapturedToken = ct;
        return ValueTask.CompletedTask;
    }
}
