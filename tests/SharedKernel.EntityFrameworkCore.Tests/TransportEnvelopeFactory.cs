using PandaBear.Messaging;
using PandaBear.Messaging.IntegrationEvents.CloudEvents;

namespace PandaBear.EntityFrameworkCore.Tests;

/// <summary>Builds integration event envelopes for transport tests.</summary>
internal static class TransportEnvelopeFactory
{
    /// <summary>Creates a JSON CloudEvents envelope for the <c>admin.tour.created</c> event with the given identifier and the current UTC time.</summary>
    /// <param name="eventId">The identifier assigned to the envelope.</param>
    /// <returns>The envelope with an empty JSON object payload.</returns>
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
