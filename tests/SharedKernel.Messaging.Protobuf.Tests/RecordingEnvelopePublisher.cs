namespace PandaBear.Messaging.Protobuf.Tests;

/// <summary>An envelope publisher that records the envelopes it receives, standing in for the generated JSON publisher.</summary>
internal sealed class RecordingEnvelopePublisher : IEventEnvelopePublisher
{
    /// <summary>Gets the received envelopes in delivery order.</summary>
    public List<EventEnvelope> Received { get; } = [];

    /// <inheritdoc />
    public ValueTask Publish(EventEnvelope envelope, CancellationToken ct)
    {
        Received.Add(envelope);
        return ValueTask.CompletedTask;
    }
}
