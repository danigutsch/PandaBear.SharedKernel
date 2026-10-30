using PandaBear.Messaging;

namespace PandaBear.Testing.Messaging;

/// <summary>
/// Envelope publisher that forwards every envelope to the wrapped publisher.
/// </summary>
/// <param name="inner">The publisher that receives the forwarded envelopes.</param>
public sealed class DelegatingEventEnvelopePublisher(IEventEnvelopePublisher inner) : IEventEnvelopePublisher
{
    /// <summary>
    /// Forwards the envelope to the wrapped publisher.
    /// </summary>
    public ValueTask Publish(EventEnvelope envelope, CancellationToken ct) => inner.Publish(envelope, ct);
}
