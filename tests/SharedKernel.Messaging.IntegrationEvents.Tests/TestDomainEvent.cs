namespace PandaBear.Messaging.IntegrationEvents.Tests;

/// <summary>
/// A domain event carrying a name, used in integration event tests.
/// </summary>
/// /// <param name="name">The name carried by the event.</param>
internal sealed class TestDomainEvent(string name) : Domain.IDomainEvent
{
    /// <summary>
    /// Gets the name carried by the event.
    /// </summary>
    public string Name { get; } = name;
}
