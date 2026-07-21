using SharedKernel.Messaging;

namespace SharedKernel.EntityFrameworkCore.Tests;

internal sealed class RecordingEventEnvelopePublisher : IEventEnvelopePublisher
{
    public List<EventEnvelope> Published { get; } = [];

    public Exception? Failure { get; init; }

    public bool FailOnce { get; init; }

    public int Attempts { get; private set; }

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
