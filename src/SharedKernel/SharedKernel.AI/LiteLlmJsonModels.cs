using System.Text.Json.Serialization;
using System.Text.Json;

namespace SharedKernel.AI;

internal sealed record LiteLlmChatCompletionRequest(
    string Model,
    IReadOnlyList<LiteLlmMessage> Messages,
    [property: JsonPropertyName("response_format")] LiteLlmResponseFormat ResponseFormat);

internal sealed record LiteLlmMessage(string Role, IReadOnlyList<LiteLlmContentPart> Content);

internal sealed record LiteLlmContentPart(
    string Type,
    string? Text,
    [property: JsonPropertyName("image_url")] LiteLlmImageUrl? ImageUrl);

internal sealed record LiteLlmImageUrl(string Url);

internal sealed record LiteLlmResponseFormat(
    string Type,
    [property: JsonPropertyName("json_schema")] LiteLlmJsonSchema JsonSchema);

internal sealed record LiteLlmJsonSchema(string Name, LiteLlmSchema Schema, bool Strict);

internal sealed record LiteLlmSchema(
    string Type,
    IReadOnlyDictionary<string, LiteLlmSchemaProperty> Properties,
    IReadOnlyList<string> Required,
    bool AdditionalProperties);

internal sealed record LiteLlmSchemaProperty
{
    public LiteLlmSchemaProperty(string type)
    {
        Type = type;
    }

    public LiteLlmSchemaProperty(IReadOnlyList<string> type)
    {
        Type = type;
    }

    public object Type { get; }
}

internal sealed record LiteLlmChatCompletionResponse(IReadOnlyList<LiteLlmChoice> Choices);

internal sealed record LiteLlmChoice(LiteLlmResponseMessage Message);

internal sealed record LiteLlmResponseMessage(string? Content);

internal sealed record GeneratedImageText(string AltText, string? Caption);

[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(LiteLlmChatCompletionRequest))]
[JsonSerializable(typeof(LiteLlmChatCompletionResponse))]
[JsonSerializable(typeof(GeneratedImageText))]
internal sealed partial class LiteLlmJsonContext : JsonSerializerContext;
