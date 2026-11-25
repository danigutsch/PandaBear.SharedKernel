using System.Text.Json.Serialization;

namespace PandaBear.Repositories;

/// <summary>Reads the strict, camel-case test taxonomy inventory.</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    RespectNullableAnnotations = true,
    RespectRequiredConstructorParameters = true,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    AllowDuplicateProperties = false)]
[JsonSerializable(typeof(TestTaxonomyInventory))]
internal sealed partial class TestTaxonomyJsonContext : JsonSerializerContext;
