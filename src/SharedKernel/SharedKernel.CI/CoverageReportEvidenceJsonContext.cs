using System.Text.Json.Serialization;

namespace SharedKernel.CI;

/// <summary>Strict source-generated JSON metadata for locally collected coverage evidence.</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true,
    NewLine = "\n",
    RespectNullableAnnotations = true,
    RespectRequiredConstructorParameters = true,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    AllowDuplicateProperties = false)]
[JsonSerializable(typeof(CoverageReportEvidenceFile))]
internal sealed partial class CoverageReportEvidenceJsonContext : JsonSerializerContext;
