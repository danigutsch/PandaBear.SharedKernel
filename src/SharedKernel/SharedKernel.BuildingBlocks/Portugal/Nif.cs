using System.Globalization;
using SharedKernel.Results;

namespace SharedKernel.BuildingBlocks;

/// <summary>
/// A canonical Portuguese tax identification number (NIF, <i>número de identificação fiscal</i>) with a valid check digit; not proof
/// that the Autoridade Tributária assigned it.
/// </summary>
/// <remarks>
/// <para>
/// A NIF is nine digits: eight sequential digits and a check digit (Decreto-Lei n.º 14/2013, art. 4.º n.º 1). The check digit is
/// modulo 11 over the first eight digits weighted 9 down to 2: with <c>r</c> the remainder of the weighted sum divided by 11, it is
/// <c>0</c> when <c>r</c> is 0 or 1 and <c>11 - r</c> otherwise.
/// </para>
/// <para>
/// The first digit tells who holds the number, and every digit but 0 is assigned: 1 to 4 to individuals (art. 4.º n.º 2), with
/// <c>45</c> for non-residents whose only Portuguese income is taxed at source (art. 4.º n.º 3); 7 to entities the Autoridade
/// Tributária numbers itself, such as undivided estates (art. 11.º n.º 3 and art. 16.º n.º 2); and, for an entity the national
/// registry of legal persons (RNPC) registers, its NIPC (art. 11.º n.º 1), whose ranges use the remaining first digits 5, 6, 8 and 9.
/// No NIF starts with 0, so such a number is refused. Source: Decreto-Lei n.º 14/2013, de 28 de janeiro,
/// <see href="https://diariodarepublica.pt/dr/detalhe/decreto-lei/14-2013-257001"/>.
/// </para>
/// </remarks>
public sealed class Nif : ValueObject
{
    /// <summary>The number of digits.</summary>
    public const int Length = 9;

    private const string Field = "nif";
    private const string NotValid = "NIF is not valid.";
    private const string Required = "NIF is required.";

    private Nif(string value) => Value = value;

    /// <summary>Gets the nine digits. An individual's number is personal data: do not log or expose it without authorization.</summary>
    public string Value { get; }

    /// <summary>
    /// Gets the value in the usual print form, three groups of three digits separated by a space (<c>123 456 789</c>); a restored value
    /// that is not nine digits is returned unchanged. <see cref="ToString"/> stays redacted.
    /// </summary>
    public string Formatted => IsNineDigits(Value)
        ? string.Create(CultureInfo.InvariantCulture, $"{Value.AsSpan(0, 3)} {Value.AsSpan(3, 3)} {Value.AsSpan(6, 3)}")
        : Value;

    /// <summary>Creates a NIF from nine ASCII digits without spaces or punctuation.</summary>
    /// <param name="value">The canonical candidate, such as <c>123456789</c>.</param>
    /// <returns>A validated NIF, or <c>NIF is not valid.</c> on the field <c>nif</c> for anything else, including a number that starts
    /// with 0 or whose check digit is wrong; the message never echoes the input.</returns>
    public static Result<Nif> Create(string? value) =>
        value is not null && IsNineDigits(value) && value[0] != '0' && value[Length - 1] - '0' == CheckDigit(value.AsSpan(0, Length - 1))
            ? Result.Ok(new Nif(value))
            : Result.Invalid<Nif>(detail: NotValid, field: Field, message: NotValid);

    /// <summary>
    /// Creates a NIF from what a person typed: nine ASCII digits in whatever grouping is familiar to them, such as <c>123456789</c>,
    /// <c>123 456 789</c> or <c>123.456.789</c>. White space, dots and dashes (hyphen, en dash, em dash and the other Unicode dash
    /// punctuation) are ignored wherever they appear; any other character, a <c>PT</c> VAT prefix included, is rejected. The number is
    /// then validated as in <see cref="Create"/>.
    /// </summary>
    /// <param name="value">The entered text.</param>
    /// <returns>A validated NIF, or a validation error on <c>nif</c>: <c>NIF is required.</c> for missing text or text with nothing but
    /// separators, and <c>NIF is not valid.</c> otherwise.</returns>
    public static Result<Nif> CreateFromInput(string? value)
    {
        var digits = value is null ? string.Empty : string.Concat(value.Where(static character => !IsSeparator(character)));
        return digits.Length == 0 ? Result.Invalid<Nif>(detail: Required, field: Field, message: Required) : Create(digits);
    }

    /// <summary>
    /// For persistence mappers: rebuilds a NIF from text this type produced earlier. It performs NO validation beyond null and blank
    /// checks and trimming, so a stored value that a newer rule rejects can still be loaded; never use it for user input.
    /// </summary>
    /// <param name="value">The stored value.</param>
    /// <returns>The NIF.</returns>
    /// <exception cref="ArgumentException"><paramref name="value"/> is <see langword="null"/>, empty or blank.</exception>
    public static Nif Restore(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return new Nif(value.Trim());
    }

    /// <inheritdoc />
    public override string ToString() => "Nif [redacted]";

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    private static bool IsNineDigits(string value) => value.Length == Length && value.All(char.IsAsciiDigit);

    private static int CheckDigit(ReadOnlySpan<char> digits)
    {
        var sum = 0;
        for (var index = 0; index < digits.Length; index++)
        {
            sum += (digits[index] - '0') * (Length - index);
        }

        var remainder = sum % 11;
        return remainder < 2 ? 0 : 11 - remainder;
    }

    private static bool IsSeparator(char character) =>
        character == '.' || char.IsWhiteSpace(character) || char.GetUnicodeCategory(character) == UnicodeCategory.DashPunctuation;
}
