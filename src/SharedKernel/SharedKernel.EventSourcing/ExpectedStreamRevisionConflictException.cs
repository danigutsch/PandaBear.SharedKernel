using System.Globalization;

namespace SharedKernel.EventSourcing;

/// <summary>
/// Represents an optimistic concurrency conflict while appending to an event stream.
/// </summary>
public sealed class ExpectedStreamRevisionConflictException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ExpectedStreamRevisionConflictException" /> class.
    /// </summary>
    public ExpectedStreamRevisionConflictException()
        : base("Expected stream revision did not match the actual stream revision.")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ExpectedStreamRevisionConflictException" /> class.
    /// </summary>
    /// <param name="message">The exception message.</param>
    public ExpectedStreamRevisionConflictException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ExpectedStreamRevisionConflictException" /> class.
    /// </summary>
    /// <param name="message">The exception message.</param>
    /// <param name="innerException">The inner exception.</param>
    public ExpectedStreamRevisionConflictException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ExpectedStreamRevisionConflictException" /> class.
    /// </summary>
    /// <param name="streamId">The stream identifier.</param>
    /// <param name="expectedRevision">The expected stream revision.</param>
    /// <param name="actualRevision">The actual stream revision, or <see langword="null" /> when the stream does not exist.</param>
    public ExpectedStreamRevisionConflictException(
        StreamId streamId,
        ExpectedStreamRevision expectedRevision,
        StreamRevision? actualRevision)
        : base(CreateMessage(streamId, expectedRevision, actualRevision))
    {
        StreamId = streamId;
        ExpectedRevision = expectedRevision;
        ActualRevision = actualRevision;
    }

    /// <summary>
    /// Gets the stream identifier.
    /// </summary>
    public StreamId StreamId { get; }

    /// <summary>
    /// Gets the expected stream revision.
    /// </summary>
    public ExpectedStreamRevision ExpectedRevision { get; } = ExpectedStreamRevision.Any;

    /// <summary>
    /// Gets the actual stream revision, or <see langword="null" /> when the stream does not exist.
    /// </summary>
    public StreamRevision? ActualRevision { get; }

    private static string CreateMessage(
        StreamId streamId,
        ExpectedStreamRevision expectedRevision,
        StreamRevision? actualRevision)
    {
        var expected = expectedRevision.RequiresEmptyStream
            ? "no stream"
            : expectedRevision.Value?.ToString(CultureInfo.InvariantCulture) ?? "any revision";
        var actual = actualRevision?.Value.ToString(CultureInfo.InvariantCulture) ?? "no stream";

        return $"Stream '{streamId.Value}' expected {expected}, but actual revision was {actual}.";
    }
}
