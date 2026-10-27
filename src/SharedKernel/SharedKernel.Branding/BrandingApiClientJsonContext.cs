using System.Text.Json;
using System.Text.Json.Serialization;

namespace PandaBear.Branding;

/// <summary>Source-generated JSON serialization context for the branding API client, so deserialization stays trim and AOT safe.</summary>
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(BrandingSettingsDto))]
internal sealed partial class BrandingApiClientJsonContext : JsonSerializerContext;
