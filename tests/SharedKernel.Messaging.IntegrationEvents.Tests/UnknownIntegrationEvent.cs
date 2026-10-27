namespace PandaBear.Messaging.IntegrationEvents.Tests;

/// <summary>Integration event fixture whose "unknown.event" type is not registered with any mapping or serializer.</summary>
/// <param name="EventId">Unique identifier of the event instance.</param>
/// <param name="OccurredAt">Moment the event occurred.</param>
internal sealed record UnknownIntegrationEvent(Guid EventId, DateTimeOffset OccurredAt) : IIntegrationEvent
{
    /// <summary>Gets the stable event type identifier "unknown.event".</summary>
    public static string EventType => "unknown.event";

    /// <summary>Gets the contract version, always 1.</summary>
    public static int EventVersion => 1;
}
