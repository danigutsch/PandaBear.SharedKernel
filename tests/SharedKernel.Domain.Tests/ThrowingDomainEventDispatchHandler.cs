namespace SharedKernel.Domain.Tests;

internal sealed class ThrowingDomainEventDispatchHandler : IDomainEventDispatchHandler
{
    public ValueTask Handle(IDomainEvent domainEvent, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        ct.ThrowIfCancellationRequested();

        return ValueTask.FromException(new InvalidOperationException("The test handler rejected the domain event."));
    }
}
