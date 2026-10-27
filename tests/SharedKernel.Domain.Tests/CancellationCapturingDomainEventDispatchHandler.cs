namespace PandaBear.Domain.Tests;

/// <summary>Test dispatch handler that remembers the cancellation token passed to its last Handle call.</summary>
internal sealed class CancellationCapturingDomainEventDispatchHandler : IDomainEventDispatchHandler
{
    /// <summary>The cancellation token received by the most recent Handle call.</summary>
    public CancellationToken CapturedToken
    {
        get; private set;
    }

    /// <summary>Stores the supplied token in CapturedToken and completes synchronously, throwing when the event is null.</summary>
    public ValueTask Handle(IDomainEvent domainEvent, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        CapturedToken = ct;
        return ValueTask.CompletedTask;
    }
}
