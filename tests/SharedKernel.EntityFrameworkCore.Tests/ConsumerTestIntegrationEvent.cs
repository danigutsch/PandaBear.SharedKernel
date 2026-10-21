using PandaBear.Messaging.IntegrationEvents;

namespace PandaBear.EntityFrameworkCore.Tests;

internal sealed record ConsumerTestIntegrationEvent(Guid EventId, DateTimeOffset OccurredAt, Guid RecordId, string Code, string Name) : IIntegrationEvent
{
    public static string EventType => "test.created";

    public static int EventVersion => 1;
}
