using PandaBear.Domain;

namespace PandaBear.EntityFrameworkCore.Tests;

internal sealed class PostgreSqlDomainEventOrder : IAggregateRoot
{
    private readonly List<IDomainEvent> domainEvents = [];

    public int Id
    {
        get; set;
    }

    public void Place() => domainEvents.Add(new PostgreSqlOrderPlacedDomainEvent(Id));

    public IReadOnlyCollection<IDomainEvent> GetDomainEvents() => domainEvents.AsReadOnly();

    public void ClearDomainEvents() => domainEvents.Clear();
}
