namespace SharedKernel.Messaging;

/// <summary>
/// Publishes transport-neutral event envelopes to the configured delivery mechanism.
/// </summary>
public interface IEventEnvelopePublisher
{
    /// <summary>
    /// Publishes an event envelope.
    /// </summary>
    /// <param name="envelope">The event envelope to publish.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A task that completes when publication finishes.</returns>
    ValueTask Publish(EventEnvelope envelope, CancellationToken ct);
}
