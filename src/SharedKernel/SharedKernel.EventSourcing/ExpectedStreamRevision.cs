namespace SharedKernel.EventSourcing;

/// <summary>
/// Represents the expected stream revision used for optimistic appends.
/// </summary>
public readonly record struct ExpectedStreamRevision
{
    private ExpectedStreamRevision(long? value, bool requiresEmptyStream)
    {
        Value = value;
        RequiresEmptyStream = requiresEmptyStream;
    }

    /// <summary>
    /// Gets the expected revision, or <see langword="null" /> when any revision is accepted.
    /// </summary>
    public long? Value { get; }

    /// <summary>
    /// Gets a value indicating whether the append expects an empty stream.
    /// </summary>
    public bool RequiresEmptyStream { get; }

    /// <summary>
    /// Gets an expectation that accepts any current stream revision.
    /// </summary>
    public static ExpectedStreamRevision Any { get; } = new(null, false);

    /// <summary>
    /// Gets an expectation that requires a missing or empty stream.
    /// </summary>
    public static ExpectedStreamRevision NoStream { get; } = new(null, true);

    /// <summary>
    /// Creates an expectation for a specific stream revision.
    /// </summary>
    /// <param name="value">The expected stream revision.</param>
    /// <returns>The created expectation.</returns>
    public static ExpectedStreamRevision From(StreamRevision value) => new(value.Value, false);
}
