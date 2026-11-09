using System.Text.Json;
using System.Text.Json.Serialization;

// These records only mirror the wire format; their members are named by the JSON properties they carry.
#pragma warning disable SKSTYLE017

namespace PandaBear.Packages;

/// <summary>The parts of the OSV API messages that the adapter reads or writes. Unknown properties are ignored.</summary>
internal static class OsvWire
{
    internal sealed record BatchRequest([property: JsonPropertyName("queries")] List<Query> Queries);

    internal sealed record Query(
        [property: JsonPropertyName("package")] PackageRef Package,
        [property: JsonPropertyName("version")] string Version,
        [property: JsonPropertyName("page_token")] string? PageToken);

    internal sealed record PackageRef(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("ecosystem")] string Ecosystem);

    internal sealed record BatchResponse([property: JsonPropertyName("results")] List<BatchResult>? Results);

    internal sealed record BatchResult(
        [property: JsonPropertyName("vulns")] List<VulnerabilityRef>? Vulns,
        [property: JsonPropertyName("next_page_token")] string? NextPageToken);

    internal sealed record VulnerabilityRef([property: JsonPropertyName("id")] string? Id);

    internal sealed record Vulnerability(
        [property: JsonPropertyName("id")] string? Id,
        [property: JsonPropertyName("withdrawn")] string? Withdrawn,
        [property: JsonPropertyName("severity")] List<SeverityEntry>? Severity,
        [property: JsonPropertyName("affected")] List<Affected>? Affected,
        [property: JsonPropertyName("database_specific")] DatabaseSpecific? DatabaseSpecific);

    internal sealed record SeverityEntry(
        [property: JsonPropertyName("type")] string? Type,
        [property: JsonPropertyName("score")] string? Score);

    internal sealed record Affected(
        [property: JsonPropertyName("package")] PackageRef? Package,
        [property: JsonPropertyName("versions")] List<string>? Versions);

    internal sealed record DatabaseSpecific([property: JsonPropertyName("severity")] JsonElement? Severity);
}
