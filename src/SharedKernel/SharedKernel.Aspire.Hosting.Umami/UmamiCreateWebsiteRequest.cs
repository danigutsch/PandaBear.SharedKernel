using System.Text.Json.Serialization;

namespace Aspire.Hosting;

/// <summary>The body of Umami's <c>POST /api/websites</c> with the website id given (Umami 3.4.1, <c>src/app/api/websites/request-schema.ts</c>).</summary>
internal sealed class UmamiCreateWebsiteRequest
{
    /// <summary>Gets the website id.</summary>
    [JsonPropertyName("id")]
    public required Guid Id { get; init; }

    /// <summary>Gets the website name, 1 to 100 characters.</summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>Gets the website's domain.</summary>
    [JsonPropertyName("domain")]
    public required string Domain { get; init; }
}
