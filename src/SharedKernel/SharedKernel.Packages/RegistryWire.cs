using System.Text.Json.Serialization;

// These records only mirror the wire format; their members are named by the JSON properties they carry.
#pragma warning disable SKSTYLE017

namespace PandaBear.Packages;

/// <summary>The parts of the crates.io and npm registry answers that the release source reads. Unknown properties are ignored.</summary>
internal static class RegistryWire
{
    internal sealed record CrateAnswer([property: JsonPropertyName("crate")] CrateInfo? Crate);

    internal sealed record CrateInfo([property: JsonPropertyName("max_stable_version")] string? MaxStableVersion);

    internal sealed record CrateVersionAnswer([property: JsonPropertyName("version")] CrateVersionInfo? Version);

    internal sealed record CrateVersionInfo([property: JsonPropertyName("created_at")] string? CreatedAt);

    internal sealed record NpmAnswer(
        [property: JsonPropertyName("dist-tags")] Dictionary<string, string>? DistTags,
        [property: JsonPropertyName("time")] Dictionary<string, string>? Time);
}
