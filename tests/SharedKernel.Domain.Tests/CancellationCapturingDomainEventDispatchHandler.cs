namespace SharedKernel.Domain.Tests;

internal sealed class CancellationCapturingDomainEventDispatchHandler : IDomainEventDispatchHandler
{
    public CancellationToken CapturedToken { get; private set; }

    public ValueTask Handle(IDomainEvent domainEvent, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        CapturedToken = ct;
        return ValueTask.CompletedTask;
    }
}
