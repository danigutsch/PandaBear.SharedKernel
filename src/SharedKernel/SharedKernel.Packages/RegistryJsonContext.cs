using System.Text.Json.Serialization;

namespace PandaBear.Packages;

/// <summary>Source-generated JSON metadata for the registry answers, so that no reflection is needed.</summary>
[JsonSerializable(typeof(RegistryWire.CrateAnswer))]
[JsonSerializable(typeof(RegistryWire.CrateVersionAnswer))]
[JsonSerializable(typeof(RegistryWire.NpmAnswer))]
internal sealed partial class RegistryJsonContext : JsonSerializerContext;
