
namespace PandaBear.Domain.EntityFrameworkCore.Tests;

internal sealed class Order : IAggregateRoot
{
    private readonly List<IDomainEvent> domainEvents = [];

    public int Id
    {
        get; set;
    }

    public void Place() => domainEvents.Add(new OrderPlacedDomainEvent(Id));

    public IReadOnlyCollection<IDomainEvent> GetDomainEvents() => domainEvents.AsReadOnly();

    public void ClearDomainEvents() => domainEvents.Clear();
}
