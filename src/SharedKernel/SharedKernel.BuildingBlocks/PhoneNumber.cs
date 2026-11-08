using PandaBear.InputNormalization;
using PandaBear.Results;

namespace PandaBear.BuildingBlocks;

/// <summary>A phone number reduced to digits with an optional leading <c>+</c>; not verified to be reachable or in service.</summary>
public sealed class PhoneNumber : ValueObject
{
    /// <summary>The minimum number of digits.</summary>
    public const int MinDigits = 7;

    /// <summary>The maximum number of digits (ITU-T E.164).</summary>
    public const int MaxDigits = 15;

    private const int MaxInputLength = 64;

    private PhoneNumber(string value) => Value = value;

    /// <summary>Gets the canonical number: digits only, preceded by <c>+</c> when the input had one. Personal data.</summary>
    public string Value
    {
        get;
    }

    /// <summary>Creates a number from text that may use spaces, dashes, dots and parentheses as separators.</summary>
    /// <param name="value">The candidate number.</param>
    /// <returns>The number or an input-independent validation error.</returns>
    public static Result<PhoneNumber> Create(string? value)
    {
        var sanitized = StringSanitizer.Sanitize(value) ?? string.Empty;
        if (sanitized.Length == 0)
        {
            return Invalid("Phone number is required.");
        }

        if (sanitized.Length > MaxInputLength)
        {
            return Invalid("Phone number is too long.");
        }

        var plus = sanitized[0] == '+';
        var digits = new char[sanitized.Length];
        var count = 0;
        for (var index = plus ? 1 : 0; index < sanitized.Length; index++)
        {
            var character = sanitized[index];
            if (char.IsAsciiDigit(character))
            {
                digits[count++] = character;
            }
            else if (character is not (' ' or '-' or '.' or '(' or ')'))
            {
                return Invalid("Phone number format is invalid.");
            }
        }

        if (count is < MinDigits or > MaxDigits)
        {
            return Invalid($"Phone number must have {MinDigits} to {MaxDigits} digits.");
        }

        return Result.Ok(new PhoneNumber((plus ? "+" : string.Empty) + new string(digits, 0, count)));
    }

    /// <summary> A value that <see cref="Create"/> would reject is kept as its sanitized text.</summary>
    /// <param name="value">The stored number.</param>
    /// <returns>The number.</returns>
    /// <exception cref="ArgumentException"><paramref name="value"/> is <see langword="null"/>, empty or blank.</exception>
    public static PhoneNumber Restore(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var sanitized = StringSanitizer.Sanitize(value);
        ArgumentException.ThrowIfNullOrEmpty(sanitized, nameof(value));
        var created = Create(sanitized);
        return created.IsSuccess ? created.Value : new PhoneNumber(sanitized);
    }

    /// <inheritdoc />
    public override string ToString() => "PhoneNumber [redacted]";

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    private static Result<PhoneNumber> Invalid(string message) =>
        Result.Invalid<PhoneNumber>(detail: message, field: "phone", message: message);
}
