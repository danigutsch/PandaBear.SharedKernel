namespace PandaBear.Messaging.IntegrationEvents.Tests;

/// <summary>Integration event fixture with the "test.event.updated" event type at version 1.</summary>
/// <param name="EventId">Unique identifier of the event instance.</param>
/// <param name="OccurredAt">Moment the event occurred.</param>
/// <param name="Name">Updated name carried by the event.</param>
internal sealed record TestUpdatedIntegrationEvent(
    Guid EventId,
    DateTimeOffset OccurredAt,
    string Name) : IIntegrationEvent
{
    /// <summary>Gets the stable event type identifier "test.event.updated".</summary>
    public static string EventType => "test.event.updated";

    /// <summary>Gets the contract version, always 1.</summary>
    public static int EventVersion => 1;
}
