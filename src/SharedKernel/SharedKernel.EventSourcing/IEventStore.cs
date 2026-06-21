namespace SharedKernel.EventSourcing;

/// <summary>
/// Defines storage-neutral event stream operations.
/// </summary>
public interface IEventStore
{
    /// <summary>
    /// Appends events to a stream with optimistic revision checking.
    /// </summary>
    ValueTask Append(
        StreamId streamId,
        ExpectedStreamRevision expectedRevision,
        IReadOnlyCollection<object> events,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads events from a stream.
    /// </summary>
    ValueTask<IReadOnlyCollection<EventEnvelope>> Load(
        StreamId streamId,
        StreamRevision? afterRevision = null,
        CancellationToken cancellationToken = default);
}
