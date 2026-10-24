using System.Text.Json.Serialization;

namespace PandaBear.CI.Tool;

/// <summary>
/// Source-generated metadata for <c>selection.json</c>, which <c>select</c> writes and <c>run</c> reads. Reading
/// rejects unknown, repeated, and missing properties.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true,
    NewLine = "\n",
    RespectNullableAnnotations = true,
    RespectRequiredConstructorParameters = true,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    AllowDuplicateProperties = false)]
[JsonSerializable(typeof(SelectionPlan))]
internal sealed partial class SelectionPlanJsonContext : JsonSerializerContext;
