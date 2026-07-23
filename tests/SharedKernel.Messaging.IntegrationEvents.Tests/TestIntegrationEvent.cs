namespace SharedKernel.Messaging.IntegrationEvents.Tests;

internal record TestIntegrationEvent(Guid EventId, DateTimeOffset OccurredAt, string Name) : IIntegrationEvent
{
    public static string EventType => "admin.tour.created";

    public static int EventVersion => 1;
}
