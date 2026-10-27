using System.Text.Json.Serialization;

namespace PandaBear.Messaging.Protobuf;

/// <summary>Source-generated JSON metadata for the extension attribute object of a decoded envelope.</summary>
[JsonSerializable(typeof(Dictionary<string, string>))]
internal sealed partial class ProtobufEventEnvelopeJsonContext : JsonSerializerContext;
