using System.Text.Json.Serialization;

namespace SharedKernel.AspNetCore.WebAnalytics;

/// <summary>The body of Umami's <c>POST /api/send</c>: an event whose payload has no name, which Umami records as a page view.</summary>
internal sealed class UmamiSendRequest
{
    /// <summary>The collection type of a page view or custom event.</summary>
    public const string EventType = "event";

    /// <summary>Gets the collection type, always <see cref="EventType"/>.</summary>
    [JsonPropertyName("type")]
    public string Type { get; } = EventType;

    /// <summary>Gets the page view.</summary>
    [JsonPropertyName("payload")]
    public required UmamiEventPayload Payload { get; init; }
}
