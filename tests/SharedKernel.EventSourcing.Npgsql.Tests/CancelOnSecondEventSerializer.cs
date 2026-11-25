namespace SharedKernel.EventSourcing.Npgsql.Tests;

/// <summary>Cancels the append at the second event's serialization boundary.</summary>
/// <param name="cancellation">The cancellation source for the append under test.</param>
internal sealed class CancelOnSecondEventSerializer(CancellationTokenSource cancellation) : IEventSerializer
{
    private readonly TestEventSerializer serializer = new();

    /// <inheritdoc />
    public string GetEventType(object eventData) => serializer.GetEventType(eventData);

    /// <inheritdoc />
    public object Deserialize(string eventType, string payloadJson) => serializer.Deserialize(eventType, payloadJson);

    /// <inheritdoc />
    public string Serialize(object eventData)
    {
        if (eventData is TestEvent { Name: "second" })
        {
            cancellation.Cancel();
        }

        return serializer.Serialize(eventData);
    }
}
