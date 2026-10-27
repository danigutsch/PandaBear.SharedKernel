namespace PandaBear.Messaging.Protobuf.Tests;

/// <summary>An envelope publisher the container creates by type, which records the envelopes it receives in a static list.</summary>
internal sealed class StaticRecordingEnvelopePublisher : IEventEnvelopePublisher
{
    /// <summary>Gets the received envelopes in delivery order.</summary>
    public static List<EventEnvelope> Received { get; } = [];

    /// <inheritdoc />
    public ValueTask Publish(EventEnvelope envelope, CancellationToken ct)
    {
        Received.Add(envelope);
        return ValueTask.CompletedTask;
    }
}
