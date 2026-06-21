namespace SharedKernel.EventSourcing;

/// <summary>
/// Represents a one-based revision in a stream.
/// </summary>
public readonly record struct StreamRevision
{
    private StreamRevision(long value) => Value = value;

    /// <summary>
    /// Gets the revision value.
    /// </summary>
    public long Value { get; }

    /// <summary>
    /// Creates a stream revision.
    /// </summary>
    /// <param name="value">The one-based revision value.</param>
    /// <returns>The created stream revision.</returns>
    public static StreamRevision From(long value)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);

        return new StreamRevision(value);
    }
}
