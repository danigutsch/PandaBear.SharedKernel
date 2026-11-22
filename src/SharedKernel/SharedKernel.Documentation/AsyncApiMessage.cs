using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;

namespace PandaBear.Documentation;

/// <summary>Describes one AsyncAPI message and the runtime JSON metadata of its payload.</summary>
/// <param name="channel">The channel key and address.</param>
/// <param name="messageId">The unique component message key.</param>
/// <param name="schemaId">The unique component payload schema key.</param>
/// <param name="name">The message name.</param>
/// <param name="title">The display title of the message.</param>
/// <param name="payload">The source-generated metadata for the production payload serializer.</param>
public sealed class AsyncApiMessage(
    string channel, string messageId, string schemaId, string name, string title, JsonTypeInfo payload)
{
    /// <summary>Gets the channel key and address.</summary>
    public string Channel { get; } = channel;

    /// <summary>Gets the unique component message key.</summary>
    public string MessageId { get; } = messageId;

    /// <summary>Gets the unique component payload schema key.</summary>
    public string SchemaId { get; } = schemaId;

    /// <summary>Gets the message name.</summary>
    public string Name { get; } = name;

    /// <summary>Gets the display title of the message.</summary>
    public string Title { get; } = title;

    /// <summary>Gets the source-generated metadata for the production payload serializer.</summary>
    public JsonTypeInfo Payload { get; } = payload;

    /// <summary>Gets the send operation if the application describes one.</summary>
    public AsyncApiOperation? Send { get; init; }

    /// <summary>Gets the receive operation if the application describes one.</summary>
    public AsyncApiOperation? Receive { get; init; }

    /// <summary>Gets the optional application-owned message header schema.</summary>
    public JsonObject? Headers { get; init; }

    /// <summary>Gets the optional message extensions, each with an <c>x-</c> key.</summary>
    public JsonObject? Extensions { get; init; }
}
