using System.Text.Json.Serialization;

namespace SharedKernel.AspNetCore.WebAnalytics;

/// <summary>Source-generated serialization of the Umami send body, so sending needs no reflection and stays trimming- and NativeAOT-safe.</summary>
[JsonSourceGenerationOptions(DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(UmamiSendRequest))]
internal sealed partial class UmamiJsonContext : JsonSerializerContext;
