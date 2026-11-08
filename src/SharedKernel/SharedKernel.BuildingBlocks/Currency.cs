using PandaBear.Results;

namespace PandaBear.BuildingBlocks;

/// <summary>An active ISO 4217 alphabetic currency code in upper case, for example <c>BRL</c>. Only codes of the ISO 4217 active list that denote money are accepted; precious metals, testing, SDR and other non-currency codes are not.</summary>
public sealed class Currency : ValueObject
{
    private const int DefaultMinorUnits = 2;

    private Currency(string code) => Code = code;

    /// <summary>Gets the Brazilian real.</summary>
    public static Currency Brl { get; } = new("BRL");

    /// <summary>Gets the euro.</summary>
    public static Currency Eur { get; } = new("EUR");

    /// <summary>Gets the United States dollar.</summary>
    public static Currency Usd { get; } = new("USD");

    /// <summary>Gets the upper-case three-letter code.</summary>
    public string Code
    {
        get;
    }

    /// <summary>Gets the number of decimal places of the currency's minor unit.</summary>
    public int MinorUnits => CurrencyCodes.MinorUnitsByCode.TryGetValue(Code, out var units) ? units : DefaultMinorUnits;

    /// <summary>Creates a currency from text; case is ignored and surrounding whitespace is removed.</summary>
    /// <param name="code">The candidate code.</param>
    /// <returns>The currency or an input-independent validation error.</returns>
    public static Result<Currency> Create(string? code)
    {
        var candidate = code?.Trim().ToUpperInvariant();
        return candidate is not null && CurrencyCodes.MinorUnitsByCode.ContainsKey(candidate)
            ? Result.Ok(new Currency(candidate))
            : Result.Invalid<Currency>(detail: "Currency is not a valid ISO 4217 currency code.", field: "currency", message: "Currency is not a valid ISO 4217 currency code.");
    }

    /// <summary>For persistence mappers: rebuilds a value from text that was produced by this type earlier; it performs NO validation beyond null checks and normalization identical to <see cref="Create"/>, so a stored value that a newer rule would reject can still be loaded; never use it for user input. A code that is no longer in the allow-list reports 2 minor units.</summary>
    /// <param name="code">The stored code.</param>
    /// <returns>The currency.</returns>
    /// <exception cref="ArgumentException"><paramref name="code"/> is <see langword="null"/>, empty or blank.</exception>
    public static Currency Restore(string code)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        return new Currency(code.Trim().ToUpperInvariant());
    }

    /// <inheritdoc />
    public override string ToString() => Code;

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Code;
    }
}
