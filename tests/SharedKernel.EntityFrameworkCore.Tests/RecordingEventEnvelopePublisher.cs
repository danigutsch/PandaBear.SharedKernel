using SharedKernel.Messaging;

namespace SharedKernel.EntityFrameworkCore.Tests;

internal sealed class RecordingEventEnvelopePublisher : IEventEnvelopePublisher
{
    public List<EventEnvelope> Published { get; } = [];

    public Exception? Failure { get; init; }

    public ValueTask Publish(EventEnvelope envelope, CancellationToken ct)
    {
        if (Failure is not null)
        {
            throw Failure;
        }

        Published.Add(envelope);
        return ValueTask.CompletedTask;
    }
}
