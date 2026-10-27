using PandaBear.Domain;

namespace PandaBear.EntityFrameworkCore.Tests;

/// <summary>
/// Dispatcher test double that records every event and optionally fails each dispatch.
/// </summary>
/// <param name="fail">True to make every dispatch throw InvalidOperationException after recording the event.</param>
internal sealed class PostgreSqlDomainEventDispatcher(bool fail) : IDomainEventDispatcher
{
    /// <summary>
    /// Events dispatched so far, in dispatch order.
    /// </summary>
    public List<IDomainEvent> Dispatched { get; } = [];

    /// <summary>
    /// Forwards the typed event to the non-generic overload so it is recorded.
    /// </summary>
    public ValueTask Dispatch<TDomainEvent>(TDomainEvent domainEvent, CancellationToken ct)
        where TDomainEvent : IDomainEvent => Dispatch((IDomainEvent)domainEvent, ct);

    /// <summary>
    /// Records the event, then throws InvalidOperationException "dispatch failed" when configured to fail.
    /// </summary>
    public ValueTask Dispatch(IDomainEvent domainEvent, CancellationToken ct)
    {
        Dispatched.Add(domainEvent);
        return fail ? throw new InvalidOperationException("dispatch failed") : ValueTask.CompletedTask;
    }
}
