using PandaBear.Results;

namespace PandaBear.BuildingBlocks;

/// <summary>An active ISO 4217 alphabetic currency code in upper case, for example <c>BRL</c>. Only codes of the ISO 4217 active list that denote money are accepted; precious metals, testing, SDR and other non-currency codes are not.</summary>
public sealed class Currency : ValueObject
{
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
    public int MinorUnits => CurrencyCodes.MinorUnitsByCode[Code];

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

    /// <inheritdoc />
    public override string ToString() => Code;

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Code;
    }
}
