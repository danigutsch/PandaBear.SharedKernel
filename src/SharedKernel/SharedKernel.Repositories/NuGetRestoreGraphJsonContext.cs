using System.Text.Json.Serialization;

namespace PandaBear.Repositories;

/// <summary>Source-generated metadata for the versioned restore graph report.</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(NuGetRestoreGraphReport))]
[JsonSerializable(typeof(NuGetUpgradeTrialReport))]
[JsonSerializable(typeof(NuGetVersionRequest))]
[JsonSerializable(typeof(NuGetVersionResponse))]
internal sealed partial class NuGetRestoreGraphJsonContext : JsonSerializerContext;
