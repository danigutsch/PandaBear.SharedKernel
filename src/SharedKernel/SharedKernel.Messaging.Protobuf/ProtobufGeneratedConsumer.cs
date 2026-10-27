namespace PandaBear.Messaging.Protobuf;

/// <summary>
/// Marks a Protobuf integration event type whose delivery the integration event source generator already emits, so the runtime consumer defers to the generated publisher.
/// </summary>
/// <param name="EventType">The stable event type identifier of the generated consumer.</param>
/// <remarks>The source generator registers this record; application code does not create it.</remarks>
public sealed record ProtobufGeneratedConsumer(string EventType);
