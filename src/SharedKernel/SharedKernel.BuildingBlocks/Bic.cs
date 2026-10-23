using PandaBear.Results;

namespace PandaBear.BuildingBlocks;

/// <summary>An ISO 9362 BIC with validated syntax; not proof of country assignment, registration or reachability.</summary>
public sealed class Bic : ValueObject
{
    private Bic(string value) => Value = value;

    /// <summary>Gets the canonical eight- or eleven-character BIC.</summary>
    public string Value
    {
        get;
    }

    /// <summary>Creates a BIC from uppercase ASCII alphanumeric text with an alphabetic country segment.</summary>
    /// <param name="value">The canonical candidate without spaces.</param>
    /// <returns>A syntactically valid BIC or an input-independent validation error.</returns>
    public static Result<Bic> Create(string? value)
    {
        if (value is null || value.Length is not (8 or 11)
            || value.Any(static character => !char.IsAsciiDigit(character) && character is not (>= 'A' and <= 'Z'))
            || value[4] is < 'A' or > 'Z'
            || value[5] is < 'A' or > 'Z')
        {
            return Result.Invalid<Bic>(detail: "Invalid canonical BIC.", field: "bic", message: "Invalid canonical BIC.");
        }

        return Result.Ok(new Bic(value));
    }

    /// <inheritdoc />
    public override string ToString() => "Bic [redacted]";

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }
}
