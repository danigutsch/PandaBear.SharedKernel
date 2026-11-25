namespace SharedKernel.EventSourcing.Npgsql.Tests;

/// <summary>Implements only the event store methods available before causation support.</summary>
internal sealed class LegacyEventStore : IEventStore
{
    /// <summary>Gets the number of calls to the original append overload.</summary>
    public int Appends { get; private set; }

    /// <inheritdoc />
    public ValueTask<IReadOnlyCollection<EventEnvelope>> Append(
        StreamId streamId, ExpectedStreamRevision expectedRevision, IReadOnlyCollection<object> events, CancellationToken ct)
    {
        Appends++;
        return ValueTask.FromResult<IReadOnlyCollection<EventEnvelope>>([]);
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyCollection<EventEnvelope>> Load(StreamId streamId, StreamRevision? afterRevision, CancellationToken ct) =>
        throw new NotSupportedException();

    /// <inheritdoc />
    public ValueTask<IReadOnlyCollection<EventEnvelope>> LoadAfter(long position, int maxCount, CancellationToken ct) =>
        throw new NotSupportedException();
}
