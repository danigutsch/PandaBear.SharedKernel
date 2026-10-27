namespace PandaBear.Messaging.Protobuf.Tests;

/// <summary>Creates CloudEvents event envelopes for the Protobuf event format tests.</summary>
internal static class ProtobufTestEnvelopes
{
    /// <summary>The occurrence time of every created envelope.</summary>
    internal static readonly DateTimeOffset Occurred = new(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Creates an envelope with fixed metadata and the given payload.</summary>
    /// <param name="payload">The payload text, or null for no payload.</param>
    /// <param name="encoding">The payload encoding.</param>
    /// <param name="dataContentType">The payload content type, or null.</param>
    /// <param name="extensionAttributesJson">The extension attribute JSON object, or null.</param>
    /// <returns>The envelope.</returns>
    internal static EventEnvelope Create(string? payload, EventPayloadEncoding encoding, string? dataContentType, string? extensionAttributesJson = null) =>
        new(
            "cloudevents",
            "1.0",
            "5d4f6a2c-0b9e-4d44-9f3e-0a4a9f4e6a11",
            new Uri("urn:sharedkernel:integration-events"),
            "admin.tour.created",
            2,
            Occurred,
            "tours/42",
            dataContentType,
            new Uri("https://schemas.example/tour-created"),
            payload,
            encoding,
            extensionAttributesJson);
}
