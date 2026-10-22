using System.Text.Json;
using System.Text.Json.Serialization;

namespace PandaBear.VaultManager.Tool.Formats.Bitwarden;

/// <summary>Source-generated JSON metadata for Bitwarden exports.</summary>
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web, WriteIndented = true)]
[JsonSerializable(typeof(BitwardenExportDto))]
[JsonSerializable(typeof(PasswordProtectedEnvelopeDto))]
internal sealed partial class BitwardenJsonContext : JsonSerializerContext;
