using System.Text.Json;
using System.Text.Json.Serialization;

namespace PandaBear.Documentation;

[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(DocumentationConformanceConfig))]
internal sealed partial class DocumentationConformanceJsonContext : JsonSerializerContext
{
}
