using PandaBear.Messaging;
using PandaBear.Messaging.IntegrationEvents.CloudEvents;

namespace PandaBear.EntityFrameworkCore.Tests;

internal static class TransportEnvelopeFactory
{
    public static EventEnvelope Create(string eventId) => new(
        CloudEventConstants.Spec,
        CloudEventConstants.SpecVersion,
        eventId,
        new Uri("urn:test:admin"),
        "admin.tour.created",
        1,
        TimeProvider.System.GetUtcNow(),
        null,
        "application/json",
        null,
        "{}",
        EventPayloadEncoding.Json,
        null);
}
