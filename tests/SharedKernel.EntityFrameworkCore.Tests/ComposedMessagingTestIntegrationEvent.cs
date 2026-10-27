using PandaBear.Messaging.IntegrationEvents;

namespace PandaBear.EntityFrameworkCore.Tests;

/// <summary>
/// Integration event used by the composed messaging registration tests.
/// </summary>
/// <param name="EventId">Unique identifier of the event.</param>
/// <param name="OccurredAt">Time at which the event occurred.</param>
internal sealed record ComposedMessagingTestIntegrationEvent(Guid EventId, DateTimeOffset OccurredAt) : IIntegrationEvent
{
    /// <summary>
    /// Event type name "sharedkernel.tests.composed.v1".
    /// </summary>
    public static string EventType => "sharedkernel.tests.composed.v1";

    /// <summary>
    /// Event schema version 1.
    /// </summary>
    public static int EventVersion => 1;
}
