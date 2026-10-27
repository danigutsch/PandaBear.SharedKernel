using PandaBear.Messaging.IntegrationEvents;

namespace PandaBear.EntityFrameworkCore.Tests;

/// <summary>
/// Integration event "test.created" carrying a record id, code and name for consumer outbox and transport tests.
/// </summary>
/// <param name="EventId">Unique identifier of the event.</param>
/// <param name="OccurredAt">Time at which the event occurred.</param>
/// <param name="RecordId">Identifier of the record the event is about.</param>
/// <param name="Code">Short code of the record.</param>
/// <param name="Name">Display name of the record.</param>
internal sealed record ConsumerTestIntegrationEvent(Guid EventId, DateTimeOffset OccurredAt, Guid RecordId, string Code, string Name) : IIntegrationEvent
{
    /// <summary>
    /// Event type name "test.created".
    /// </summary>
    public static string EventType => "test.created";

    /// <summary>
    /// Event schema version 1.
    /// </summary>
    public static int EventVersion => 1;
}
