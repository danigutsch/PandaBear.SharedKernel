namespace SharedKernel.EventSourcing;

/// <summary>
/// Identifies an event stream.
/// </summary>
public readonly record struct StreamId
{
    private readonly string? normalizedValue;

    private StreamId(string value) => normalizedValue = value;

    /// <summary>
    /// Gets the normalized stream identifier.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the stream identifier was not created through <see cref="From" />.</exception>
    public string Value => normalizedValue ?? throw new InvalidOperationException(
        "StreamId must be created through StreamId.From before it can be used.");

    /// <summary>
    /// Creates a stream identifier from a non-empty value.
    /// </summary>
    /// <param name="value">The stream identifier value.</param>
    /// <returns>The created stream identifier.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value" /> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="value" /> is empty or whitespace.</exception>
    public static StreamId From(string? value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Stream identifiers cannot be empty or whitespace.", nameof(value));
        }

        return new StreamId(value.Trim());
    }

    /// <inheritdoc />
    public override string ToString() => Value;
}
