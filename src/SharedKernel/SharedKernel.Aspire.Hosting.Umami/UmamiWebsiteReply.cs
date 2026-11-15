using System.Text.Json.Serialization;

namespace Aspire.Hosting;

/// <summary>The part of Umami's website answer that provisioning reads (Umami 3.4.1, <c>src/app/api/websites/[websiteId]/route.ts</c>).</summary>
internal sealed class UmamiWebsiteReply
{
    /// <summary>Gets the website id.</summary>
    [JsonPropertyName("id")]
    public Guid? Id { get; init; }
}
