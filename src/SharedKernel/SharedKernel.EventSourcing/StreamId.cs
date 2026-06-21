namespace SharedKernel.EventSourcing;

/// <summary>
/// Identifies an event stream.
/// </summary>
public readonly record struct StreamId
{
    private StreamId(string value) => Value = value;

    /// <summary>
    /// Gets the normalized stream identifier.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Creates a stream identifier from a non-empty value.
    /// </summary>
    /// <param name="value">The stream identifier value.</param>
    /// <returns>The created stream identifier.</returns>
    public static StreamId From(string? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        return new StreamId(value.Trim());
    }

    /// <inheritdoc />
    public override string ToString() => Value;
}
