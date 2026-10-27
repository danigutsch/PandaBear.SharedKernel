namespace PandaBear.Messaging.IntegrationEvents.Tests;

/// <summary>Base integration event fixture carrying a tour name and the "admin.tour.created" event type at version 1.</summary>
/// <param name="EventId">Unique identifier of the event instance.</param>
/// <param name="OccurredAt">Moment the event occurred.</param>
/// <param name="Name">Name copied from the originating domain event.</param>
internal record TestIntegrationEvent(Guid EventId, DateTimeOffset OccurredAt, string Name) : IIntegrationEvent
{
    /// <summary>Gets the stable event type identifier "admin.tour.created".</summary>
    public static string EventType => "admin.tour.created";

    /// <summary>Gets the contract version, always 1.</summary>
    public static int EventVersion => 1;
}
