namespace SharedKernel.Messaging.IntegrationEvents.Tests;

internal sealed record TestUpdatedIntegrationEvent(
    Guid EventId,
    DateTimeOffset OccurredAt,
    string Name) : IIntegrationEvent
{
    public static string EventType => "test.event.updated";

    public static int EventVersion => 1;
}
