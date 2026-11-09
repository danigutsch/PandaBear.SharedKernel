using System.Text.Json;
using System.Text.Json.Serialization;

namespace PandaBear.Testing.Tests;

/// <summary>Source-generated metadata of <see cref="TourPayload"/> with the web defaults.</summary>
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(TourPayload))]
internal sealed partial class TourPayloadJsonContext : JsonSerializerContext;
