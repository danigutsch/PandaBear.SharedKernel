namespace SharedKernel.Idempotency;

/// <summary>
/// Represents the namespace in which idempotency keys are unique.
/// </summary>
public readonly record struct IdempotencyScope
{
    private readonly string? normalizedValue;

    private IdempotencyScope(string value)
    {
        normalizedValue = value;
    }

    /// <summary>
    /// Gets the normalized scope value.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the scope was not created through <see cref="From" />.</exception>
    public string Value => normalizedValue ?? throw new InvalidOperationException(
        "IdempotencyScope must be created through IdempotencyScope.From before it can be used.");

    /// <summary>
    /// Creates an idempotency scope from a non-empty value.
    /// </summary>
    /// <param name="value">The scope value.</param>
    /// <returns>The created idempotency scope.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value" /> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="value" /> is empty or whitespace.</exception>
    public static IdempotencyScope From(string? value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Idempotency scopes cannot be empty or whitespace.", nameof(value));
        }

        return new IdempotencyScope(value.Trim());
    }

    /// <inheritdoc />
    public override string ToString() => Value;
}
