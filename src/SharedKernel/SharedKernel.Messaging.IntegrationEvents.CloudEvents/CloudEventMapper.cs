namespace PandaBear.Messaging.IntegrationEvents.CloudEvents;

/// <summary>
/// Maps typed integration events to CloudEvents metadata envelopes.
/// </summary>
public static class CloudEventMapper
{
    /// <summary>
    /// Maps a typed integration event to a CloudEvents metadata envelope.
    /// </summary>
    /// <typeparam name="TIntegrationEvent">The integration event type.</typeparam>
    /// <param name="integrationEvent">The integration event instance.</param>
    /// <param name="metadata">The transport metadata for the CloudEvent.</param>
    /// <returns>A CloudEvents metadata envelope containing the typed payload.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="integrationEvent"/> or <paramref name="metadata"/> is <see langword="null"/>.</exception>
    public static CloudEventEnvelope<TIntegrationEvent> ToCloudEvent<TIntegrationEvent>(
        TIntegrationEvent integrationEvent,
        CloudEventMetadata metadata)
        where TIntegrationEvent : IIntegrationEvent
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        ArgumentNullException.ThrowIfNull(metadata);

        return new CloudEventEnvelope<TIntegrationEvent>(
            integrationEvent.EventId.ToString("D"),
            metadata.Source,
            TIntegrationEvent.EventType,
            CloudEventConstants.SpecVersion,
            integrationEvent.OccurredAt,
            metadata.Subject,
            metadata.DataContentType,
            metadata.DataSchema,
            TIntegrationEvent.EventVersion,
            integrationEvent);
    }
}
