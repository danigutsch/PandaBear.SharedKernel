namespace PandaBear.Messaging.IntegrationEvents.Tests;

/// <summary>Holds the attribute-registered mapping from the test domain event to its integration event.</summary>
internal static class TestIntegrationEventMappings
{
    /// <summary>Maps a test domain event to a <see cref="TestIntegrationEvent"/>, copying its name and using the supplied id and timestamp.</summary>
    [IntegrationEventMapping]
    internal static TestIntegrationEvent Map(TestDomainEvent domainEvent, Guid eventId, DateTimeOffset occurredAt) =>
        new(eventId, occurredAt, domainEvent.Name);
}
