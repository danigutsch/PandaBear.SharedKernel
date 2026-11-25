using System.Globalization;
using SharedKernel.Domain;
using SharedKernel.Results;

namespace SharedKernel.BuildingBlocks;

/// <summary>A canonical Brazilian CPF with validated check digits; not proof of registration or identity.</summary>
public sealed class Cpf : ValueObject
{
    private const int MaskLength = 14;

    private Cpf(string value) => Value = value;

    /// <summary>Gets the sensitive, eleven-digit value. Do not log or expose it without authorization.</summary>
    public string Value { get; }

    /// <summary>Creates a CPF from eleven ASCII digits without display punctuation.</summary>
    /// <param name="value">The canonical candidate.</param>
    /// <returns>A validated CPF or an input-independent validation error.</returns>
    public static Result<Cpf> Create(string? value)
    {
        if (value is null || value.Length != 11
            || value.Any(static character => !char.IsAsciiDigit(character))
            || value.AsSpan().IndexOfAnyExcept(value[0]) < 0
            || value[9] - '0' != BrazilianCheckDigits.Calculate(value.AsSpan(0, 9), 10)
            || value[10] - '0' != BrazilianCheckDigits.Calculate(value.AsSpan(0, 10), 11))
        {
            return Result.Invalid<Cpf>(detail: "CPF is not valid.", field: "cpf", message: "CPF is not valid.");
        }

        return Result.Ok(new Cpf(value));
    }

    /// <summary>
    /// Creates a CPF from what a person typed: eleven ASCII digits in whatever grouping is familiar to them, such as <c>52998224725</c>,
    /// the display mask <c>529.982.247-25</c> or <c>529 982 247 25</c>. White space, dots and dashes (hyphen, en dash, em dash and the other
    /// Unicode dash punctuation) are ignored wherever they appear; any other character is rejected. The check digits are validated as in
    /// <see cref="Create"/>.
    /// </summary>
    /// <param name="value">The entered text.</param>
    /// <returns>
    /// A validated CPF, or an input-independent validation error on <c>cpf</c>: <c>CPF is required.</c> for missing text or text with
    /// nothing but separators, and <c>CPF is not valid.</c> otherwise.
    /// </returns>
    public static Result<Cpf> CreateFromInput(string? value)
    {
        var digits = value is null ? string.Empty : string.Concat(value.Where(static character => !IsSeparator(character)));
        return digits.Length == 0
            ? Result.Invalid<Cpf>(detail: "CPF is required.", field: "cpf", message: "CPF is required.")
            : Create(digits);
    }

    /// <summary>Gets the sensitive value in the display mask <c>000.000.000-00</c>; a restored value that is not eleven digits is returned unchanged. Personal data: do not log or expose it without authorization. <see cref="ToString"/> stays redacted.</summary>
    public string Formatted => Value.Length == 11 && Value.All(char.IsAsciiDigit)
        ? string.Create(MaskLength, Value, static (span, value) =>
        {
            value.AsSpan(0, 3).CopyTo(span);
            span[3] = '.';
            value.AsSpan(3, 3).CopyTo(span[4..]);
            span[7] = '.';
            value.AsSpan(6, 3).CopyTo(span[8..]);
            span[11] = '-';
            value.AsSpan(9, 2).CopyTo(span[12..]);
        })
        : Value;

    /// <summary>For persistence mappers: rebuilds a CPF from text that was produced by this type earlier; it performs NO validation beyond null/blank checks and trimming, so a stored value that a newer rule would reject can still be loaded; never use it for user input.</summary>
    /// <param name="value">The stored value.</param>
    /// <returns>The CPF.</returns>
    /// <exception cref="ArgumentException"><paramref name="value"/> is <see langword="null"/>, empty or blank.</exception>
    public static Cpf Restore(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return new Cpf(value.Trim());
    }

    /// <inheritdoc />
    public override string ToString() => "Cpf [redacted]";

    private static bool IsSeparator(char character) =>
        character == '.' || char.IsWhiteSpace(character) || char.GetUnicodeCategory(character) == UnicodeCategory.DashPunctuation;

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }
}
