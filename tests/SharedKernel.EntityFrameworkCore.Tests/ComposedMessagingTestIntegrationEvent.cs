using PandaBear.Messaging.IntegrationEvents;

namespace PandaBear.EntityFrameworkCore.Tests;

internal sealed record ComposedMessagingTestIntegrationEvent(Guid EventId, DateTimeOffset OccurredAt) : IIntegrationEvent
{
    public static string EventType => "sharedkernel.tests.composed.v1";

    public static int EventVersion => 1;
}
