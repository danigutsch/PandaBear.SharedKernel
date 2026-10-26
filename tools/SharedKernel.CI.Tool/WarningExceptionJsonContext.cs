using System.Text.Json.Serialization;

namespace PandaBear.CI.Tool;

/// <summary>
/// Source-generated metadata for the exception ledger. Reading rejects unknown, repeated, and missing properties.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    RespectNullableAnnotations = true,
    RespectRequiredConstructorParameters = true,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    AllowDuplicateProperties = false)]
[JsonSerializable(typeof(WarningExceptionLedger))]
internal sealed partial class WarningExceptionJsonContext : JsonSerializerContext;
