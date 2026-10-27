using PandaBear.Messaging;

namespace PandaBear.EntityFrameworkCore.Tests;

/// <summary>
/// Envelope publisher that stores published envelopes and can be set to fail every call or only the first.
/// </summary>
internal sealed class RecordingEventEnvelopePublisher : IEventEnvelopePublisher
{
    /// <summary>
    /// Envelopes published successfully, in order.
    /// </summary>
    public List<EventEnvelope> Published { get; } = [];

    /// <summary>
    /// Exception thrown by publish calls, or null to succeed.
    /// </summary>
    public Exception? Failure
    {
        get; init;
    }

    /// <summary>
    /// True to throw Failure only on the first call instead of every call.
    /// </summary>
    public bool FailOnce
    {
        get; init;
    }

    /// <summary>
    /// Number of Publish calls made, including failed ones.
    /// </summary>
    public int Attempts
    {
        get; private set;
    }

    /// <summary>
    /// Counts the attempt, throws Failure when configured to, and otherwise stores the envelope.
    /// </summary>
    public ValueTask Publish(EventEnvelope envelope, CancellationToken ct)
    {
        Attempts++;
        if (Failure is not null && (!FailOnce || Attempts == 1))
        {
            throw Failure;
        }

        Published.Add(envelope);
        return ValueTask.CompletedTask;
    }
}
