using System.Text.RegularExpressions;

namespace SharedKernel.Idempotency;

/// <summary>
/// Represents a stable idempotency key within a scope.
/// </summary>
public readonly partial record struct IdempotencyKey
{
    private readonly string? normalizedValue;

    private IdempotencyKey(string value)
    {
        normalizedValue = value;
    }

    /// <summary>
    /// Gets the normalized key value.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the key was not created through <see cref="From" />.</exception>
    public string Value => normalizedValue ?? throw new InvalidOperationException(
        "IdempotencyKey must be created through IdempotencyKey.From before it can be used.");

    /// <summary>
    /// Creates an idempotency key from an opaque, high-entropy value.
    /// </summary>
    /// <param name="value">The key value. UUIDs and random token strings are recommended.</param>
    /// <returns>The created idempotency key.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value" /> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="value" /> is empty, whitespace, longer than 255 characters, or contains characters outside the supported token format.</exception>
    public static IdempotencyKey From(string? value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Idempotency keys cannot be empty or whitespace.", nameof(value));
        }

        var trimmedValue = value.Trim();
        if (!KeyFormatRegex().IsMatch(trimmedValue))
        {
            throw new ArgumentException(
                "Idempotency keys must be 1 to 255 characters and contain only letters, digits, '.', '_', ':', or '-'.",
                nameof(value));
        }

        return new IdempotencyKey(trimmedValue);
    }

    /// <inheritdoc />
    public override string ToString() => Value;

    [GeneratedRegex(@"^[A-Za-z0-9._:-]{1,255}$", RegexOptions.CultureInvariant)]
    private static partial Regex KeyFormatRegex();
}
