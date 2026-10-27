using PandaBear.Messaging.IntegrationEvents;

namespace PandaBear.EntityFrameworkCore.Tests;

/// <summary>
/// Serializer stub that returns a fixed payload for any integration event, letting tests tell which context's serializer ran.
/// </summary>
/// <param name="payload">The payload returned by every Serialize call.</param>
internal sealed class ContextQualifiedMessagingTestIntegrationEventSerializer(string payload)
    : IIntegrationEventSerializer
{
    /// <summary>
    /// Payload used by the first context, the JSON object {"context":"first"}.
    /// </summary>
    public const string FirstPayload = "{\"context\":\"first\"}";
    /// <summary>
    /// Payload used by the second context, the JSON object {"context":"second"}.
    /// </summary>
    public const string SecondPayload = "{\"context\":\"second\"}";

    /// <summary>
    /// Rejects a null event and returns the configured payload.
    /// </summary>
    public string Serialize<TIntegrationEvent>(TIntegrationEvent integrationEvent)
        where TIntegrationEvent : IIntegrationEvent
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        return payload;
    }
}
