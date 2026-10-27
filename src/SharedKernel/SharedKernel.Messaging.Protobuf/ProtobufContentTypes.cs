namespace PandaBear.Messaging.Protobuf;

/// <summary>
/// Defines the media types that identify Protobuf in an integration-event envelope.
/// </summary>
public static class ProtobufContentTypes
{
    /// <summary>
    /// The <c>datacontenttype</c> of an event payload that is a serialized Protobuf message (<c>application/protobuf</c>).
    /// </summary>
    public const string Payload = "application/protobuf";

    /// <summary>
    /// The media type of a whole event in the CloudEvents Protobuf event format (<c>application/cloudevents+protobuf</c>).
    /// </summary>
    public const string EventFormat = "application/cloudevents+protobuf";
}
