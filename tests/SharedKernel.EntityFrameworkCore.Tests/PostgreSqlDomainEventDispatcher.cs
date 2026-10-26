using PandaBear.Domain;

namespace PandaBear.EntityFrameworkCore.Tests;

internal sealed class PostgreSqlDomainEventDispatcher(bool fail) : IDomainEventDispatcher
{
    public List<IDomainEvent> Dispatched { get; } = [];

    public ValueTask Dispatch<TDomainEvent>(TDomainEvent domainEvent, CancellationToken ct)
        where TDomainEvent : IDomainEvent => Dispatch((IDomainEvent)domainEvent, ct);

    public ValueTask Dispatch(IDomainEvent domainEvent, CancellationToken ct)
    {
        Dispatched.Add(domainEvent);
        return fail ? throw new InvalidOperationException("dispatch failed") : ValueTask.CompletedTask;
    }
}
