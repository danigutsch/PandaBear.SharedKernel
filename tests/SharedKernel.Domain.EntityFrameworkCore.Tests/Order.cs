
namespace PandaBear.Domain.EntityFrameworkCore.Tests;

/// <summary>
/// Test aggregate root that collects OrderPlacedDomainEvent instances until they are cleared.
/// </summary>
internal sealed class Order : IAggregateRoot
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
    /// Records an OrderPlacedDomainEvent carrying the order Id.
    /// </summary>
    public void Place() => domainEvents.Add(new OrderPlacedDomainEvent(Id));

    /// <summary>
    /// Returns a read-only view of the events recorded so far.
    /// </summary>
    public IReadOnlyCollection<IDomainEvent> GetDomainEvents() => domainEvents.AsReadOnly();

    /// <summary>
    /// Discards all recorded domain events.
    /// </summary>
    public void ClearDomainEvents() => domainEvents.Clear();
}
