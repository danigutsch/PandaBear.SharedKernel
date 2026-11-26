using SharedKernel.Messaging;

namespace SharedKernel.EntityFrameworkCore.Tests;

/// <summary>Runs the optional test probe before the normally registered transport publisher.</summary>
/// <param name="probe">The shared publish-attempt recorder or failure probe.</param>
/// <param name="transport">The scoped producer from the existing keyed transport registration.</param>
internal sealed class ProbedTransportPublisher(RecordingEventEnvelopePublisher probe, IEventEnvelopePublisher transport)
    : IEventEnvelopePublisher
{
    /// <inheritdoc />
    public async ValueTask Publish(EventEnvelope envelope, CancellationToken ct)
    {
        await probe.Publish(envelope, ct);
        await transport.Publish(envelope, ct);
    }
}
