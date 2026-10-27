using PandaBear.Messaging;
using PandaBear.Messaging.IntegrationEvents;

namespace PandaBear.EntityFrameworkCore.Tests;

/// <summary>
/// Serializer stub that reports a fixed binary payload with the Base64 encoding and the <c>application/protobuf</c> content type.
/// </summary>
internal sealed class Base64TestIntegrationEventSerializer : IIntegrationEventSerializer
{
    /// <summary>
    /// Returns the fixed base64 payload text.
    /// </summary>
    public string Serialize<TIntegrationEvent>(TIntegrationEvent integrationEvent)
        where TIntegrationEvent : IIntegrationEvent => "CgRBbHBz";

    /// <summary>
    /// Returns the fixed base64 payload with its encoding and content type.
    /// </summary>
    public SerializedIntegrationEventPayload SerializePayload<TIntegrationEvent>(TIntegrationEvent integrationEvent)
        where TIntegrationEvent : IIntegrationEvent =>
        new(Serialize(integrationEvent), EventPayloadEncoding.Base64, "application/protobuf");
}
