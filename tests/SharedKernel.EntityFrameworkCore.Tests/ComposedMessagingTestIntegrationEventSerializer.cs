using PandaBear.Messaging.IntegrationEvents;

namespace PandaBear.EntityFrameworkCore.Tests;

/// <summary>
/// Serializer stub that rejects null events and always returns the empty JSON object.
/// </summary>
internal sealed class ComposedMessagingTestIntegrationEventSerializer : IIntegrationEventSerializer
{
    /// <summary>
    /// Returns "{}" for any non-null integration event.
    /// </summary>
    public string Serialize<TIntegrationEvent>(TIntegrationEvent integrationEvent)
        where TIntegrationEvent : IIntegrationEvent
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        return "{}";
    }
}
