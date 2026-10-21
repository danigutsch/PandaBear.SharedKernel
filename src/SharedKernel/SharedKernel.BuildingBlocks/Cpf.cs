using PandaBear.Results;

namespace PandaBear.BuildingBlocks;

/// <summary>A canonical Brazilian CPF with validated check digits; not proof of registration or identity.</summary>
public sealed class Cpf : ValueObject
{
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
            return Result.Invalid<Cpf>(detail: "Invalid canonical CPF.", field: "cpf", message: "Invalid canonical CPF.");
        }

        return Result.Ok(new Cpf(value));
    }

    /// <inheritdoc />
    public override string ToString() => "Cpf [redacted]";

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }
}
