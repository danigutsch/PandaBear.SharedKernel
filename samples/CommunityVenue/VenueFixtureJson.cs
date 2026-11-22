using System.Text.Json.Serialization;

namespace CommunityVenue;

/// <summary>Serialization metadata for the local sample content.</summary>
[JsonSerializable(typeof(VenuePage[]))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
internal sealed partial class VenueFixtureJson : JsonSerializerContext;
