using System.Text.Json;
using System.Text.Json.Serialization;

namespace PandaBear.Documentation;

/// <summary>Source-generated JSON serialization context for the conformance configuration, so loading it stays trim and AOT safe.</summary>
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(DocumentationConformanceConfig))]
internal sealed partial class DocumentationConformanceJsonContext : JsonSerializerContext
{
}
