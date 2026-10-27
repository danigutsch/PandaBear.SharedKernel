using System.Text.Json;
using System.Text.Json.Serialization;

namespace PandaBear.Documentation;

/// <summary>Source-generated JSON serialization context for the generator configuration, so loading it stays trim and AOT safe.</summary>
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(DocumentationGeneratorConfig))]
internal sealed partial class DocumentationGeneratorJsonContext : JsonSerializerContext
{
}
