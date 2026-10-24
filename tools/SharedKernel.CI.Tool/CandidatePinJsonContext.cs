using System.Text.Json.Serialization;

namespace PandaBear.CI.Tool;

/// <summary>
/// Source-generated JSON metadata for candidate manifests. Names are matched exactly, comments and trailing commas are
/// rejected, a property given twice is rejected, and the identity properties are required and may not be null. Evidence
/// properties, which consumption runs add, are ignored.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    RespectNullableAnnotations = true,
    RespectRequiredConstructorParameters = true,
    AllowDuplicateProperties = false)]
[JsonSerializable(typeof(CandidatePin))]
internal sealed partial class CandidatePinJsonContext : JsonSerializerContext;
