using PandaBear.Messaging;

namespace PandaBear.Testing.Messaging;

/// <summary>
/// Envelope publisher the container creates by type, which records the envelopes it receives in a static list.
/// </summary>
public sealed class StaticRecordingEventEnvelopePublisher : IEventEnvelopePublisher
{
    private static readonly List<EventEnvelope> ReceivedEnvelopes = [];

    /// <summary>
    /// Gets the received envelopes in delivery order.
    /// </summary>
    public static IList<EventEnvelope> Received => ReceivedEnvelopes;

    /// <inheritdoc />
    public ValueTask Publish(EventEnvelope envelope, CancellationToken ct)
    {
        ReceivedEnvelopes.Add(envelope);
        return ValueTask.CompletedTask;
    }
}
