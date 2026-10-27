using System.Text.Json;

namespace PandaBear.EventSourcing.Npgsql.Tests;

/// <summary>An event serializer that supports only TestEvent, using System.Text.Json and a single event type name.</summary>
internal sealed class TestEventSerializer : IEventSerializer
{
    /// <summary>The event type name that identifies a serialized TestEvent.</summary>
    public const string EventType = "test.event.v1";

    /// <summary>Returns the test event type name for a TestEvent and throws InvalidOperationException for any other payload.</summary>
    public string GetEventType(object eventData)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        return eventData is TestEvent ? EventType : throw new InvalidOperationException("Unsupported event type.");
    }

    /// <summary>Serializes a TestEvent to its JSON payload.</summary>
    public string Serialize(object eventData)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        return JsonSerializer.Serialize((TestEvent)eventData);
    }

    /// <summary>Deserializes a JSON payload to a TestEvent, throwing InvalidOperationException for an unknown event type or a payload that deserializes to null.</summary>
    public object Deserialize(string eventType, string payloadJson)
    {
        return !string.Equals(EventType, eventType, StringComparison.Ordinal)
            ? throw new InvalidOperationException($"Unsupported event type '{eventType}'.")
            : JsonSerializer.Deserialize<TestEvent>(payloadJson)
            ?? throw new InvalidOperationException("Event payload could not be deserialized.");
    }
}
