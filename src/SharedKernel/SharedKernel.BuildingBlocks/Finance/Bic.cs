using PandaBear.Results;

namespace PandaBear.BuildingBlocks;

/// <summary>An ISO 9362 BIC with validated syntax; not proof of country assignment, registration or reachability.</summary>
public sealed class Bic : ValueObject
{
    private Bic(string value) => Value = value;

    /// <summary>Gets the canonical eight- or eleven-character BIC.</summary>
    public string Value { get; }

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
            return Result.Invalid<Bic>(detail: "BIC is not valid.", field: "bic", message: "BIC is not valid.");
        }

        return Result.Ok(new Bic(value));
    }

    /// <summary>
    /// For persistence mappers: rebuilds a BIC from text that was produced by this type earlier; it performs NO validation beyond
    /// null/blank checks and trimming, so a stored value that a newer rule would reject can still be loaded; never use it for user input.
    /// </summary>
    /// <param name="value">The stored value.</param>
    /// <returns>The BIC.</returns>
    /// <exception cref="ArgumentException"><paramref name="value"/> is <see langword="null"/>, empty or blank.</exception>
    public static Bic Restore(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return new Bic(value.Trim());
    }

    /// <inheritdoc />
    public override string ToString() => "Bic [redacted]";

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }
}
