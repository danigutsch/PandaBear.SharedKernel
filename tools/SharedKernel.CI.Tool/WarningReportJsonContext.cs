using System.Text.Json.Serialization;

namespace PandaBear.CI.Tool;

/// <summary>
/// Source-generated metadata for the warning report, which verification steps write and <c>warning-summary</c> reads.
/// Reading rejects unknown, repeated, and missing properties.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    NewLine = "\n",
    RespectNullableAnnotations = true,
    RespectRequiredConstructorParameters = true,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    AllowDuplicateProperties = false)]
[JsonSerializable(typeof(WarningReport))]
internal sealed partial class WarningReportJsonContext : JsonSerializerContext;
