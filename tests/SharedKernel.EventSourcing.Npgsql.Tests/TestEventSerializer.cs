using System.Text.Json;

namespace SharedKernel.EventSourcing.Npgsql.Tests;

internal sealed class TestEventSerializer : IEventSerializer
{
    public const string EventType = "test.event.v1";

    public string GetEventType(object eventData)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        return eventData is TestEvent ? EventType : throw new InvalidOperationException("Unsupported event type.");
    }

    public string Serialize(object eventData)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        return JsonSerializer.Serialize((TestEvent)eventData);
    }

    public object Deserialize(string eventType, string payloadJson)
    {
        return !string.Equals(EventType, eventType, StringComparison.Ordinal)
            ? throw new InvalidOperationException($"Unsupported event type '{eventType}'.")
            : JsonSerializer.Deserialize<TestEvent>(payloadJson)
            ?? throw new InvalidOperationException("Event payload could not be deserialized.");
    }
}
