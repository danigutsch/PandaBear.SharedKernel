using System.Text.Json.Serialization;

namespace PandaBear.Documentation.Tests;

/// <summary>The source-generated venue event serializer.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(VenueOpeningNotice))]
[JsonSerializable(typeof(VenueClosingNotice))]
internal sealed partial class VenueNoticeJsonContext : JsonSerializerContext;
