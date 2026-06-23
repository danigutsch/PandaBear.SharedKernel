namespace SharedKernel.EventSourcing;

/// <summary>
/// Represents one event read from or appended to an event stream.
/// </summary>
/// <param name="StreamId">The stream identifier.</param>
/// <param name="Position">The global event-store position.</param>
/// <param name="Revision">The stream revision for the event.</param>
/// <param name="EventId">The stable event identifier.</param>
/// <param name="EventType">The event type name.</param>
/// <param name="Data">The event payload.</param>
/// <param name="RecordedAt">The time the event was recorded.</param>
public sealed record EventEnvelope(
    StreamId StreamId,
    long Position,
    StreamRevision Revision,
    Guid EventId,
    string EventType,
    object Data,
    DateTimeOffset RecordedAt);
