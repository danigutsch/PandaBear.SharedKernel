namespace PandaBear.Messaging.IntegrationEvents.CloudEvents.Tests;

/// <summary>
/// Integration event with type "admin.tour.created" and version 1, used to test CloudEvents mapping.
/// </summary>
/// <param name="EventId">The unique identifier of the event.</param>
/// <param name="OccurredAt">The time the event occurred.</param>
/// <param name="Name">The name of the created tour.</param>
internal sealed record TestIntegrationEvent(Guid EventId, DateTimeOffset OccurredAt, string Name) : IIntegrationEvent
{
    /// <summary>
    /// Gets the event type "admin.tour.created".
    /// </summary>
    public static string EventType => "admin.tour.created";

    /// <summary>
    /// Gets the event version 1.
    /// </summary>
    public static int EventVersion => 1;
}
