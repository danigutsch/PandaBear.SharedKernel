namespace SharedKernel.Domain;

/// <summary>Dispatches each domain event through registered handlers in registration order.</summary>
public sealed class CompositeDomainEventDispatcher : IDomainEventDispatcher
{
    private readonly IReadOnlyList<IDomainEventDispatchHandler> handlers;

    /// <summary>Initializes a composite dispatcher with the registered domain-event handlers.</summary>
    /// <param name="handlers">The handlers invoked for every domain event.</param>
    public CompositeDomainEventDispatcher(IEnumerable<IDomainEventDispatchHandler> handlers)
    {
        ArgumentNullException.ThrowIfNull(handlers);

        this.handlers = handlers.ToArray();
    }

    /// <inheritdoc />
    public ValueTask Dispatch<TDomainEvent>(TDomainEvent domainEvent, CancellationToken ct)
        where TDomainEvent : IDomainEvent
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return Dispatch((IDomainEvent)domainEvent, ct);
    }

    /// <inheritdoc />
    public async ValueTask Dispatch(IDomainEvent domainEvent, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        foreach (var handler in handlers)
        {
            await handler.Handle(domainEvent, ct).ConfigureAwait(false);
        }
    }
}
