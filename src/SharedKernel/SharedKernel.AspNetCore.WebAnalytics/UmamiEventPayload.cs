using System.Text.Json.Serialization;

namespace SharedKernel.AspNetCore.WebAnalytics;

/// <summary>
/// The payload of a page view in Umami's documented send body. It has no <c>name</c>, so Umami records a page view, and no <c>title</c>,
/// <c>screen</c>, <c>data</c> or <c>id</c>, which the server does not know or must not send. Absent values are left out.
/// </summary>
internal sealed class UmamiEventPayload
{
    /// <summary>Gets the Umami website id.</summary>
    [JsonPropertyName("website")]
    public required Guid Website { get; init; }

    /// <summary>Gets the host name of the page.</summary>
    [JsonPropertyName("hostname")]
    public string? Hostname { get; init; }

    /// <summary>Gets the path of the page with only its <c>utm_*</c> query values.</summary>
    [JsonPropertyName("url")]
    public required string Url { get; init; }

    /// <summary>Gets the referring page on another site, without query and fragment.</summary>
    [JsonPropertyName("referrer")]
    public string? Referrer { get; init; }

    /// <summary>Gets the visitor's first language tag.</summary>
    [JsonPropertyName("language")]
    public string? Language { get; init; }
}
