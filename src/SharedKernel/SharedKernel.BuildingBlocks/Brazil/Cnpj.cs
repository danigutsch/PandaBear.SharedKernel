using PandaBear.Results;

namespace PandaBear.BuildingBlocks;

/// <summary>A canonical numeric or alphanumeric Brazilian CNPJ; not proof of registration or identity.</summary>
public sealed class Cnpj : ValueObject
{
    private Cnpj(string value) => Value = value;

    /// <summary>Gets the sensitive, fourteen-character value. Do not log or expose it without authorization.</summary>
    public string Value { get; }

    /// <summary>Creates a CNPJ from twelve uppercase ASCII alphanumeric characters and two check digits.</summary>
    /// <param name="value">The canonical candidate without display punctuation.</param>
    /// <returns>A validated CNPJ or an input-independent validation error.</returns>
    public static Result<Cnpj> Create(string? value)
    {
        if (value is null || value.Length != 14
            || value.Any(static character => !char.IsAsciiDigit(character) && character is not (>= 'A' and <= 'Z'))
            || !char.IsAsciiDigit(value[12])
            || !char.IsAsciiDigit(value[13])
            || value.AsSpan().IndexOfAnyExcept(value[0]) < 0
            || value[12] - '0' != BrazilianCheckDigits.Calculate(value.AsSpan(0, 12), 5)
            || value[13] - '0' != BrazilianCheckDigits.Calculate(value.AsSpan(0, 13), 6))
        {
            return Result.Invalid<Cnpj>(detail: "Invalid canonical CNPJ.", field: "cnpj", message: "Invalid canonical CNPJ.");
        }

        return Result.Ok(new Cnpj(value));
    }

    /// <inheritdoc />
    public override string ToString() => "Cnpj [redacted]";

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }
}
