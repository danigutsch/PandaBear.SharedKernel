using Google.Protobuf.WellKnownTypes;
using PandaBear.Messaging.IntegrationEvents;

namespace PandaBear.Messaging.Protobuf.Tests;

/// <summary>The integration event contract of the <c>TourCreated</c> message that protoc generates from <c>Protos/tour_created.proto</c>.</summary>
internal sealed partial class TourCreated : IIntegrationEvent
{
    /// <summary>Gets the event type "test.tour.created".</summary>
    public static string EventType => "test.tour.created";

    /// <summary>Gets the event version 1.</summary>
    public static int EventVersion => 1;

    /// <summary>Gets the event identifier parsed from <see cref="Id"/>.</summary>
    public Guid EventId => Guid.Parse(Id);

    /// <summary>Gets the time the event occurred, converted from <see cref="Occurred"/>.</summary>
    public DateTimeOffset OccurredAt => Occurred.ToDateTimeOffset();

    /// <summary>Creates an event from its CLR values.</summary>
    /// <param name="eventId">The event identifier.</param>
    /// <param name="occurredAt">The time the event occurred.</param>
    /// <param name="name">The name of the created tour.</param>
    /// <returns>The event.</returns>
    public static TourCreated Create(Guid eventId, DateTimeOffset occurredAt, string name) =>
        new()
        {
            Id = eventId.ToString("D"),
            Occurred = Timestamp.FromDateTimeOffset(occurredAt),
            Name = name
        };
}
