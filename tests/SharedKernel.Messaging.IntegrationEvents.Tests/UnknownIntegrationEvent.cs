namespace SharedKernel.Messaging.IntegrationEvents.Tests;

internal sealed record UnknownIntegrationEvent(Guid EventId, DateTimeOffset OccurredAt) : IIntegrationEvent
{
    public static string EventType => "unknown.event";

    public static int EventVersion => 1;
}
