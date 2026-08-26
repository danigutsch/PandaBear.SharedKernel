using SharedKernel.Messaging.IntegrationEvents;

namespace SharedKernel.EntityFrameworkCore.Tests;

internal sealed class ComposedMessagingTestIntegrationEventSerializer : IIntegrationEventSerializer
{
    public string Serialize<TIntegrationEvent>(TIntegrationEvent integrationEvent)
        where TIntegrationEvent : IIntegrationEvent
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        return "{}";
    }
}
