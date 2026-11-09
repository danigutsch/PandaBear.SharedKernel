using System.Text.Json;

namespace PandaBear.Packages.Tests;

/// <summary>Parses OSV records for tests.</summary>
internal static class OsvRecords
{
    /// <summary>Parses a vulnerability record.</summary>
    public static OsvWire.Vulnerability Parse(string json) => JsonSerializer.Deserialize(json, OsvJsonContext.Default.Vulnerability)!;
}
