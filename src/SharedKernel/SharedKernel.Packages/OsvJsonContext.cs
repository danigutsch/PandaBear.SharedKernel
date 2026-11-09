using System.Text.Json.Serialization;

namespace PandaBear.Packages;

/// <summary>Source-generated JSON metadata for the OSV messages, so that no reflection is needed.</summary>
[JsonSourceGenerationOptions(DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(OsvWire.BatchRequest))]
[JsonSerializable(typeof(OsvWire.BatchResponse))]
[JsonSerializable(typeof(OsvWire.Vulnerability))]
internal sealed partial class OsvJsonContext : JsonSerializerContext;
