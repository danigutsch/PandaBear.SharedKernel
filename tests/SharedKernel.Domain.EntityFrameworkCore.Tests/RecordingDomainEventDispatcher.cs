
namespace PandaBear.Domain.EntityFrameworkCore.Tests;

internal sealed class RecordingDomainEventDispatcher : IDomainEventDispatcher
{
    public List<IDomainEvent> Dispatched { get; } = [];

    public ValueTask Dispatch<TDomainEvent>(TDomainEvent domainEvent, CancellationToken ct)
        where TDomainEvent : IDomainEvent => Dispatch((IDomainEvent)domainEvent, ct);

    public ValueTask Dispatch(IDomainEvent domainEvent, CancellationToken ct)
    {
        Dispatched.Add(domainEvent);
        return ValueTask.CompletedTask;
    }
}
