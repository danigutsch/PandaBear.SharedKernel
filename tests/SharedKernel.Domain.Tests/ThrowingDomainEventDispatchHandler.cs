namespace PandaBear.Domain.Tests;

/// <summary>Test dispatch handler that always fails with an InvalidOperationException.</summary>
internal sealed class ThrowingDomainEventDispatchHandler : IDomainEventDispatchHandler
{
    /// <summary>Returns a faulted ValueTask carrying InvalidOperationException 'The test handler rejected the domain event.', after throwing on a null event or an already cancelled token.</summary>
    public ValueTask Handle(IDomainEvent domainEvent, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        ct.ThrowIfCancellationRequested();

        return ValueTask.FromException(new InvalidOperationException("The test handler rejected the domain event."));
    }
}
