using Google.Protobuf.WellKnownTypes;
using PandaBear.Messaging.IntegrationEvents;

namespace PandaBear.Messaging.Protobuf.TestContracts;

/// <summary>The integration event contract of the <c>OrderPlaced</c> message that protoc generates from <c>Protos/order_placed.proto</c>.</summary>
public sealed partial class OrderPlaced : IIntegrationEvent
{
    /// <summary>Gets the event type "test.order.placed".</summary>
    public static string EventType => "test.order.placed";

    /// <summary>Gets the event version 1.</summary>
    public static int EventVersion => 1;

    /// <summary>Gets the event identifier parsed from <see cref="Id"/>.</summary>
    public Guid EventId => Guid.Parse(Id);

    /// <summary>Gets the time the event occurred, converted from <see cref="Occurred"/>.</summary>
    public DateTimeOffset OccurredAt => Occurred.ToDateTimeOffset();

    /// <summary>Creates an event from its CLR values.</summary>
    /// <param name="eventId">The event identifier.</param>
    /// <param name="occurredAt">The time the event occurred.</param>
    /// <param name="orderNumber">The number of the placed order.</param>
    /// <returns>The event.</returns>
    public static OrderPlaced Create(Guid eventId, DateTimeOffset occurredAt, string orderNumber) =>
        new()
        {
            Id = eventId.ToString("D"),
            Occurred = Timestamp.FromDateTimeOffset(occurredAt),
            OrderNumber = orderNumber
        };
}
