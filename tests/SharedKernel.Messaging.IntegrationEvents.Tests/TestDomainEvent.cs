namespace SharedKernel.Messaging.IntegrationEvents.Tests;

internal sealed class TestDomainEvent(string name) : Domain.IDomainEvent
{
    public string Name { get; } = name;
}
