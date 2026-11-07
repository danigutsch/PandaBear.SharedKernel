using System.Globalization;
using PandaBear.Results;

namespace PandaBear.BuildingBlocks;

/// <summary>An amount of one <see cref="Currency"/>, rounded away from zero to the currency's minor unit. No currency conversion is performed.</summary>
public sealed class Money : ValueObject
{
    private Money(decimal amount, Currency currency)
    {
        Amount = amount;
        Currency = currency;
    }

    /// <summary>Gets the amount, already rounded to the currency's minor unit.</summary>
    public decimal Amount
    {
        get;
    }

    /// <summary>Gets the currency.</summary>
    public Currency Currency
    {
        get;
    }

    /// <summary>Gets a value indicating whether the amount is greater than zero.</summary>
    public bool IsPositive => Amount > 0;

    /// <summary>Creates a money value, rounding the amount away from zero to the currency's minor unit.</summary>
    /// <param name="amount">The amount.</param>
    /// <param name="currency">The currency.</param>
    /// <returns>The money value or a validation error when the currency is missing.</returns>
    public static Result<Money> Create(decimal amount, Currency? currency)
    {
        if (currency is null)
        {
            return Result.Invalid<Money>(detail: "Currency is required.", field: "currency", message: "Currency is required.");
        }

        return Result.Ok(new Money(Math.Round(amount, currency.MinorUnits, MidpointRounding.AwayFromZero), currency));
    }

    /// <summary>Creates a money value that must already have no more decimals than the currency's minor unit; the amount is never rounded.</summary>
    /// <param name="amount">The amount.</param>
    /// <param name="currency">The currency.</param>
    /// <param name="field">The field name used in the validation error.</param>
    /// <param name="label">The label used in the validation message, for example <c>Base price</c>.</param>
    /// <returns>The money value, or a validation error when the currency is missing or the amount has too many decimals.</returns>
    public static Result<Money> CreateExact(decimal amount, Currency? currency, string field = "amount", string label = "Amount")
    {
        if (currency is null)
        {
            return Create(amount, currency);
        }

        if (Math.Round(amount, currency.MinorUnits, MidpointRounding.AwayFromZero) != amount)
        {
            var message = currency.MinorUnits == 0
                ? $"{label} must be a whole number for {currency.Code}."
                : $"{label} cannot have more than {currency.MinorUnits} decimal places for {currency.Code}.";
            return Result.Invalid<Money>(detail: message, field: field, message: message);
        }

        return Result.Ok(new Money(amount, currency));
    }

    /// <summary>Adds another amount of the same currency.</summary>
    /// <param name="other">The amount to add.</param>
    /// <returns>The sum or a validation error when the currencies differ.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <see langword="null"/>.</exception>
    public Result<Money> Add(Money other)
    {
        ArgumentNullException.ThrowIfNull(other);

        return other.Currency == Currency
            ? Result.Ok(new Money(Amount + other.Amount, Currency))
            : Result.Invalid<Money>(detail: "Currencies must match.", field: "currency", message: "Currencies must match.");
    }

    /// <inheritdoc />
    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"{Currency.Code} {Amount.ToString("F" + Currency.MinorUnits.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture)}");

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Amount;
        yield return Currency;
    }
}
