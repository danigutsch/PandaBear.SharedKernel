using SharedKernel.Messaging.IntegrationEvents;

namespace SharedKernel.EntityFrameworkCore.Tests;

internal sealed class ContextQualifiedMessagingTestIntegrationEventSerializer(string payload)
    : IIntegrationEventSerializer
{
    public const string FirstPayload = "{\"context\":\"first\"}";
    public const string SecondPayload = "{\"context\":\"second\"}";

    public string Serialize<TIntegrationEvent>(TIntegrationEvent integrationEvent)
        where TIntegrationEvent : IIntegrationEvent
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        return payload;
    }
}
