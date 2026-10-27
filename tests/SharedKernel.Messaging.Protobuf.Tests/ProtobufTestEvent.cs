using Google.Protobuf;
using Google.Protobuf.Reflection;
using Google.Protobuf.WellKnownTypes;
using PandaBear.Messaging.IntegrationEvents;

namespace PandaBear.Messaging.Protobuf.Tests;

/// <summary>
/// An integration event written by hand as a Protobuf message with three fields: name (1, string), event id (2, string) and occurrence time in Unix milliseconds (3, int64).
/// </summary>
internal sealed class ProtobufTestEvent : IIntegrationEvent, IMessage<ProtobufTestEvent>
{
    /// <summary>Gets the event type "test.protobuf.created".</summary>
    public static string EventType => "test.protobuf.created";

    /// <summary>Gets the event version 1.</summary>
    public static int EventVersion => 1;

    /// <summary>Gets or sets the event identifier.</summary>
    public Guid EventId
    {
        get; set;
    }

    /// <summary>Gets or sets the time the event occurred, with millisecond precision on the wire.</summary>
    public DateTimeOffset OccurredAt
    {
        get; set;
    }

    /// <summary>Gets or sets the name carried by the event.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets a placeholder descriptor: no generated descriptor exists for this hand-written message, and the tests never use it.</summary>
    public MessageDescriptor Descriptor => StringValue.Descriptor;

    /// <inheritdoc />
    public int CalculateSize() =>
        CodedOutputStream.ComputeStringSize(Name) + 1
        + CodedOutputStream.ComputeStringSize(EventId.ToString("D")) + 1
        + CodedOutputStream.ComputeInt64Size(OccurredAt.ToUnixTimeMilliseconds()) + 1;

    /// <inheritdoc />
    public void WriteTo(CodedOutputStream output)
    {
        output.WriteRawTag(10);
        output.WriteString(Name);
        output.WriteRawTag(18);
        output.WriteString(EventId.ToString("D"));
        output.WriteRawTag(24);
        output.WriteInt64(OccurredAt.ToUnixTimeMilliseconds());
    }

    /// <inheritdoc />
    public void MergeFrom(CodedInputStream input)
    {
        uint tag;
        while ((tag = input.ReadTag()) != 0)
        {
            switch (tag)
            {
                case 10:
                    Name = input.ReadString();
                    break;
                case 18:
                    EventId = Guid.Parse(input.ReadString());
                    break;
                case 24:
                    OccurredAt = DateTimeOffset.FromUnixTimeMilliseconds(input.ReadInt64());
                    break;
                default:
                    input.SkipLastField();
                    break;
            }
        }
    }

    /// <inheritdoc />
    public void MergeFrom(ProtobufTestEvent message)
    {
        Name = message.Name;
        EventId = message.EventId;
        OccurredAt = message.OccurredAt;
    }

    /// <inheritdoc />
    public ProtobufTestEvent Clone() => new() { Name = Name, EventId = EventId, OccurredAt = OccurredAt };

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as ProtobufTestEvent);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Name, EventId, OccurredAt.ToUnixTimeMilliseconds());

    /// <inheritdoc />
    public bool Equals(ProtobufTestEvent? other) =>
        other is not null && Name == other.Name && EventId == other.EventId && OccurredAt.ToUnixTimeMilliseconds() == other.OccurredAt.ToUnixTimeMilliseconds();
}
