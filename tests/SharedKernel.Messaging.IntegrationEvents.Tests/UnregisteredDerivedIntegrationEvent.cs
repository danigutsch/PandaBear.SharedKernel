namespace SharedKernel.Messaging.IntegrationEvents.Tests;

internal sealed record UnregisteredDerivedIntegrationEvent(
    Guid EventId,
    DateTimeOffset OccurredAt,
    string Name,
    string Details) : TestIntegrationEvent(EventId, OccurredAt, Name);
