using PandaBear.Messaging.IntegrationEvents;

namespace PandaBear.Messaging.Protobuf.Tests;

/// <summary>An integration event that is not a Protobuf message.</summary>
/// <param name="EventId">The event identifier.</param>
/// <param name="OccurredAt">The time the event occurred.</param>
internal sealed record JsonOnlyTestEvent(Guid EventId, DateTimeOffset OccurredAt) : IIntegrationEvent
{
    /// <summary>Gets the event type "test.json.created".</summary>
    public static string EventType => "test.json.created";

    /// <summary>Gets the event version 1.</summary>
    public static int EventVersion => 1;
}
