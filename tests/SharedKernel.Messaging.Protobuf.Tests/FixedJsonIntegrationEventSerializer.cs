using SharedKernel.Messaging.IntegrationEvents;

namespace SharedKernel.Messaging.Protobuf.Tests;

/// <summary>A serializer that returns a fixed JSON text and keeps the default encoding and content type.</summary>
internal sealed class FixedJsonIntegrationEventSerializer : IIntegrationEventSerializer
{
    /// <inheritdoc />
    public string Serialize<TIntegrationEvent>(TIntegrationEvent integrationEvent)
        where TIntegrationEvent : IIntegrationEvent => """{"fixed":true}""";
}
