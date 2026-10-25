using System.Text.Json;
using System.Text.Json.Serialization;

namespace PandaBear.Branding;

[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(BrandingSettingsDto))]
internal sealed partial class BrandingApiClientJsonContext : JsonSerializerContext;
