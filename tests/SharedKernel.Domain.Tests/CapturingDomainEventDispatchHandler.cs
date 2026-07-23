namespace SharedKernel.Domain.Tests;

internal sealed class CapturingDomainEventDispatchHandler(string name, List<string> calls) : IDomainEventDispatchHandler
{
    public ValueTask Handle(IDomainEvent domainEvent, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        ct.ThrowIfCancellationRequested();

        calls.Add(name);
        return ValueTask.CompletedTask;
    }
}
