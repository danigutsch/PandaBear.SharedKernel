// Adapted from ViajantesTurismo. See NOTICE.ViajantesTurismo.txt for the MIT license and attribution.

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;

namespace PandaBear.Documentation;

/// <summary>Renders and verifies generated AsyncAPI 3 JSON artifacts.</summary>
public static class AsyncApiDocument
{
    private const string ContentType = "application/json";
    private static readonly JsonSerializerOptions DocumentOptions = new() { WriteIndented = true, NewLine = "\n" };

    /// <summary>
    /// Renders JSON messages in ordinal channel and message ID order, using the supplied serializer metadata for payload schemas.
    /// </summary>
    /// <param name="title">The document title.</param>
    /// <param name="version">The version of the documented contract.</param>
    /// <param name="description">The document description.</param>
    /// <param name="messages">The documented messages. Messages on the same channel share its channel definition.</param>
    /// <param name="payloadOptions">Optional schema exporter settings for the application contracts.</param>
    /// <param name="extensions">Optional document extensions, each with an <c>x-</c> key.</param>
    /// <returns>Indented AsyncAPI 3.0.0 JSON with a trailing line feed.</returns>
    /// <exception cref="ArgumentNullException">A required argument, message, or payload is null.</exception>
    /// <exception cref="ArgumentException">Required text is blank, component or operation keys are duplicated, or an extension key is invalid.</exception>
    public static string Render(
        string title, string version, string description, IEnumerable<AsyncApiMessage> messages,
        JsonSchemaExporterOptions? payloadOptions = null, JsonObject? extensions = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentNullException.ThrowIfNull(messages);

        var channels = new JsonObject();
        var operations = new JsonObject();
        var components = new JsonObject();
        var schemas = new JsonObject();
        foreach (var message in messages.Select(Validate)
                     .OrderBy(message => message.Channel, StringComparer.Ordinal)
                     .ThenBy(message => message.MessageId, StringComparer.Ordinal))
        {
            var channel = message.Channel;
            if (channels[channel] is not JsonObject channelNode)
            {
                channelNode = new JsonObject { ["address"] = channel, ["messages"] = new JsonObject() };
                channels.Add(channel, channelNode);
            }

            var localKey = JsonNamingPolicy.CamelCase.ConvertName(message.MessageId);
            var channelPointer = $"#/channels/{PointerSegment(channel)}";
            var channelMessagePointer = $"{channelPointer}/messages/{PointerSegment(localKey)}";
            channelNode["messages"]!.AsObject().Add(localKey, Reference($"#/components/messages/{PointerSegment(message.MessageId)}"));

            AddOperation(operations, message.Send, "send", channelPointer, channelMessagePointer);
            AddOperation(operations, message.Receive, "receive", channelPointer, channelMessagePointer);
            components.Add(message.MessageId, CreateMessage(message));
            schemas.Add(message.SchemaId, CreatePayload(message, payloadOptions));
        }

        var root = new JsonObject
        {
            ["asyncapi"] = "3.0.0",
            ["info"] = new JsonObject { ["title"] = title, ["version"] = version, ["description"] = description },
            ["defaultContentType"] = ContentType,
            ["channels"] = channels,
            ["operations"] = operations,
            ["components"] = new JsonObject { ["messages"] = components, ["schemas"] = schemas },
        };
        AddExtensions(root, extensions);
        return root.ToJsonString(DocumentOptions) + "\n";
    }

    /// <summary>Checks whether an existing artifact contains the generated text, without modifying it.</summary>
    /// <param name="path">The path to the generated artifact.</param>
    /// <param name="generated">The expected artifact contents.</param>
    /// <returns>Whether the file exists and its decoded text exactly matches the expected text.</returns>
    /// <exception cref="ArgumentException">The path is null or empty.</exception>
    /// <exception cref="ArgumentNullException">The expected text is null.</exception>
    /// <exception cref="IOException">The artifact could not be read.</exception>
    /// <exception cref="UnauthorizedAccessException">Access to the artifact was denied.</exception>
    public static bool MatchesFile(string path, string generated)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentNullException.ThrowIfNull(generated);
        return File.Exists(path) && string.Equals(File.ReadAllText(path), generated, StringComparison.Ordinal);
    }

    private static AsyncApiMessage Validate(AsyncApiMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentException.ThrowIfNullOrWhiteSpace(message.Channel);
        ArgumentException.ThrowIfNullOrWhiteSpace(message.MessageId);
        ArgumentException.ThrowIfNullOrWhiteSpace(message.SchemaId);
        ArgumentException.ThrowIfNullOrWhiteSpace(message.Name);
        ArgumentException.ThrowIfNullOrWhiteSpace(message.Title);
        ArgumentNullException.ThrowIfNull(message.Payload);
        return message;
    }

    private static void AddOperation(JsonObject operations, AsyncApiOperation? operation, string action, string channel, string message)
    {
        if (operation is null)
        {
            return;
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(operation.Id);
        ArgumentException.ThrowIfNullOrWhiteSpace(operation.Summary);
        operations.Add(operation.Id, new JsonObject
        {
            ["action"] = action,
            ["channel"] = Reference(channel),
            ["messages"] = new JsonArray(Reference(message)),
            ["summary"] = operation.Summary,
        });
    }

    private static JsonObject CreateMessage(AsyncApiMessage message)
    {
        var node = new JsonObject
        {
            ["name"] = message.Name,
            ["title"] = message.Title,
            ["contentType"] = ContentType,
        };
        if (message.Headers is not null)
        {
            node.Add("headers", message.Headers.DeepClone());
        }

        node.Add("payload", Reference(SchemaPointer(message.SchemaId)));
        AddExtensions(node, message.Extensions);
        return node;
    }

    private static JsonNode CreatePayload(AsyncApiMessage message, JsonSchemaExporterOptions? options)
    {
        var pointer = SchemaPointer(message.SchemaId);
        return message.Payload.GetJsonSchemaAsNode(new JsonSchemaExporterOptions
        {
            TreatNullObliviousAsNonNullable = options?.TreatNullObliviousAsNonNullable ?? false,
            TransformSchemaNode = (context, node) =>
            {
                if (node is JsonObject schema && schema["$ref"] is JsonValue value
                    && value.TryGetValue<string>(out var reference)
                    && (reference == "#" || reference.StartsWith("#/", StringComparison.Ordinal)))
                {
                    schema["$ref"] = pointer + reference[1..];
                }

                return options?.TransformSchemaNode?.Invoke(context, node) ?? node;
            },
        });
    }

    private static void AddExtensions(JsonObject target, JsonObject? extensions)
    {
        if (extensions is null)
        {
            return;
        }

        foreach (var (key, value) in extensions)
        {
            if (key.Length <= 2 || !key.StartsWith("x-", StringComparison.Ordinal))
            {
                throw new ArgumentException("AsyncAPI extension keys must start with 'x-'.", nameof(extensions));
            }

            target.Add(key, value?.DeepClone());
        }
    }

    private static string SchemaPointer(string schemaId) => $"#/components/schemas/{PointerSegment(schemaId)}";

    private static string PointerSegment(string value) => Uri.EscapeDataString(
        value.Replace("~", "~0", StringComparison.Ordinal).Replace("/", "~1", StringComparison.Ordinal));

    private static JsonObject Reference(string pointer) => new() { ["$ref"] = pointer };
}
