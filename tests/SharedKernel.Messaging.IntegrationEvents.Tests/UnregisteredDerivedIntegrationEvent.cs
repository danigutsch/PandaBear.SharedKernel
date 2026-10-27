namespace PandaBear.Messaging.IntegrationEvents.Tests;

/// <summary>Subtype of <see cref="TestIntegrationEvent"/> that adds details but is not itself registered, inheriting the base event type.</summary>
/// <param name="EventId">Unique identifier of the event instance.</param>
/// <param name="OccurredAt">Moment the event occurred.</param>
/// <param name="Name">Name passed to the base event.</param>
/// <param name="Details">Extra detail text present only on the derived type.</param>
internal sealed record UnregisteredDerivedIntegrationEvent(
    Guid EventId,
    DateTimeOffset OccurredAt,
    string Name,
    string Details) : TestIntegrationEvent(EventId, OccurredAt, Name);
