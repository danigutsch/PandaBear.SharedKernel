using Microsoft.Extensions.DependencyInjection;

namespace PandaBear.Messaging.Protobuf;

/// <summary>Binds one Protobuf integration event type to the delegate that deserializes its payload and runs its handler.</summary>
/// <param name="EventType">The stable event type identifier.</param>
/// <param name="Handle">Delivers an envelope of that event type to its handler in a new scope.</param>
internal sealed record ProtobufConsumerRegistration(
    string EventType,
    Func<IServiceScopeFactory, EventEnvelope, CancellationToken, ValueTask> Handle);
