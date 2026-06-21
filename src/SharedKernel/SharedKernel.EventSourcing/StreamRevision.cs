namespace SharedKernel.EventSourcing;

/// <summary>
/// Represents a one-based revision in a stream.
/// </summary>
public readonly record struct StreamRevision
{
    private readonly long? value;

    private StreamRevision(long value) => this.value = value;

    /// <summary>
    /// Gets the revision value.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the revision was not created through <see cref="From" />.</exception>
    public long Value => value ?? throw new InvalidOperationException(
        "StreamRevision must be created through StreamRevision.From before it can be used.");

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
