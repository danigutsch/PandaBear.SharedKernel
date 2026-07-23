namespace SharedKernel.Messaging.IntegrationEvents.Tests;

internal static class TestIntegrationEventMappings
{
    [IntegrationEventMapping]
    internal static TestIntegrationEvent Map(TestDomainEvent domainEvent, Guid eventId, DateTimeOffset occurredAt) =>
        new(eventId, occurredAt, domainEvent.Name);
}
