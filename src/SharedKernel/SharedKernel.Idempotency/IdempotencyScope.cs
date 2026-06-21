namespace SharedKernel.Idempotency;

/// <summary>
/// Represents the namespace in which idempotency keys are unique.
/// </summary>
public readonly record struct IdempotencyScope
{
    private IdempotencyScope(string value)
    {
        Value = value;
    }

    /// <summary>
    /// Gets the normalized scope value.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Creates an idempotency scope from a non-empty value.
    /// </summary>
    /// <param name="value">The scope value.</param>
    /// <returns>The created idempotency scope.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="value" /> is null, empty, or whitespace.</exception>
    public static IdempotencyScope From(string? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        return new IdempotencyScope(value.Trim());
    }

    /// <inheritdoc />
    public override string ToString() => Value;
}
