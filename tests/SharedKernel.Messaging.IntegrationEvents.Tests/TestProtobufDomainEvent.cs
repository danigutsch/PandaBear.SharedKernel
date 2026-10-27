namespace PandaBear.Messaging.IntegrationEvents.Tests;

/// <summary>
/// A domain event carrying a name, mapped to a Protobuf integration event in generated serializer tests.
/// </summary>
/// <param name="name">The name carried by the event.</param>
internal sealed class TestProtobufDomainEvent(string name) : Domain.IDomainEvent
{
    /// <summary>
    /// Gets the name carried by the event.
    /// </summary>
    public string Name { get; } = name;
}
