using PandaBear.Domain;

namespace PandaBear.EntityFrameworkCore.Tests;

/// <summary>
/// Test aggregate root that collects PostgreSqlOrderPlacedDomainEvent instances until they are cleared.
/// </summary>
internal sealed class PostgreSqlDomainEventOrder : IAggregateRoot
{
    private readonly List<IDomainEvent> domainEvents = [];

    /// <summary>
    /// Identifier of the order.
    /// </summary>
    public int Id
    {
        get; set;
    }

    /// <summary>
    /// Records a PostgreSqlOrderPlacedDomainEvent carrying the order Id.
    /// </summary>
    public void Place() => domainEvents.Add(new PostgreSqlOrderPlacedDomainEvent(Id));

    /// <summary>
    /// Returns a read-only view of the events recorded so far.
    /// </summary>
    public IReadOnlyCollection<IDomainEvent> GetDomainEvents() => domainEvents.AsReadOnly();

    /// <summary>
    /// Discards all recorded domain events.
    /// </summary>
    public void ClearDomainEvents() => domainEvents.Clear();
}
