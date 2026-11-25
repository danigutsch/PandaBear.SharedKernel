namespace PandaBear.EventSourcing;

/// <summary>
/// Defines storage-neutral event stream operations.
/// </summary>
public interface IEventStore
{
    /// <summary>
    /// Appends events to a stream with optimistic revision checking.
    /// </summary>
    ValueTask<IReadOnlyCollection<EventEnvelope>> Append(
        StreamId streamId,
        ExpectedStreamRevision expectedRevision,
        IReadOnlyCollection<object> events,
        CancellationToken ct);

    /// <summary>Appends events and records the exact event that caused the append.</summary>
    /// <param name="streamId">The stream to append to.</param>
    /// <param name="expectedRevision">The expected current revision.</param>
    /// <param name="events">The batch to append in order.</param>
    /// <param name="causationEventId">The nonempty identifier of the event that caused this batch.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The committed events with their recorded causation.</returns>
    /// <exception cref="NotSupportedException">The provider does not support event causation.</exception>
    ValueTask<IReadOnlyCollection<EventEnvelope>> Append(
        StreamId streamId,
        ExpectedStreamRevision expectedRevision,
        IReadOnlyCollection<object> events,
        Guid causationEventId,
        CancellationToken ct) => ValueTask.FromException<IReadOnlyCollection<EventEnvelope>>(
            new NotSupportedException("This event store does not record event causation."));

    /// <summary>
    /// Loads events from a stream.
    /// </summary>
    ValueTask<IReadOnlyCollection<EventEnvelope>> Load(
        StreamId streamId,
        StreamRevision? afterRevision,
        CancellationToken ct);

    /// <summary>
    /// Loads events after a global event-store position.
    /// </summary>
    ValueTask<IReadOnlyCollection<EventEnvelope>> LoadAfter(
        long position,
        int maxCount,
        CancellationToken ct);
}
