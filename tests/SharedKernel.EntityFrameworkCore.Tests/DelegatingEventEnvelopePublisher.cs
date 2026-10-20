using PandaBear.Messaging;

namespace PandaBear.EntityFrameworkCore.Tests;

internal sealed class DelegatingEventEnvelopePublisher(IEventEnvelopePublisher inner) : IEventEnvelopePublisher
{
    public ValueTask Publish(EventEnvelope envelope, CancellationToken ct) => inner.Publish(envelope, ct);
}
