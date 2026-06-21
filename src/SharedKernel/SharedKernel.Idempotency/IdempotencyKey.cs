namespace SharedKernel.Idempotency;

/// <summary>
/// Represents a stable idempotency key within a scope.
/// </summary>
public readonly record struct IdempotencyKey
{
    private IdempotencyKey(string value)
    {
        Value = value;
    }

    /// <summary>
    /// Gets the normalized key value.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Creates an idempotency key from a non-empty value.
    /// </summary>
    /// <param name="value">The key value.</param>
    /// <returns>The created idempotency key.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="value" /> is null, empty, or whitespace.</exception>
    public static IdempotencyKey From(string? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        return new IdempotencyKey(value.Trim());
    }

    /// <inheritdoc />
    public override string ToString() => Value;
}
