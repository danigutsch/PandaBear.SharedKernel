using System.Globalization;
using SharedKernel.Domain;
using SharedKernel.Results;

namespace SharedKernel.BuildingBlocks;

/// <summary>
/// An amount of one <see cref="Currency"/>, rounded away from zero to the currency's minor unit. No currency conversion is performed.
/// <see cref="ToString()"/> is the invariant <c>EUR 1.00</c> form for logs and storage; <see cref="ToDisplayString(IFormatProvider)"/> and the <c>C</c>
/// format are for people.
/// </summary>
public sealed class Money : ValueObject, IFormattable
{
    private const string DisplayFormat = "C";

    private Money(decimal amount, Currency currency)
    {
        Amount = amount;
        Currency = currency;
    }

    /// <summary>Gets the amount, already rounded to the currency's minor unit.</summary>
    public decimal Amount { get; }

    /// <summary>Gets the currency.</summary>
    public Currency Currency { get; }

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
    /// <remarks>For an amount a person typed, use <see cref="CreateFromInput"/>, which also counts the decimal places it was written with.</remarks>
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

    /// <summary>
    /// Creates money from an amount a person typed, in a form or a request body: like <see cref="CreateExact"/>, but an amount written
    /// with more decimal places than the currency's minor unit is refused even when the extra places are zeros. For EUR, <c>1.500</c> is
    /// refused, not read as 1.50, because it is most likely 1500 typed with a thousands separator. The amount is never rounded.
    /// </summary>
    /// <remarks>
    /// A <see cref="decimal"/> keeps the decimal places it was written with (its scale): <c>decimal.Parse("1.500")</c> and the JSON number
    /// <c>1.500</c> read by System.Text.Json both have three. Pass the amount as it was parsed. Arithmetic changes the scale, so a computed
    /// amount such as <c>1000.00m * 0.30m</c>, which has four, belongs in <see cref="CreateExact"/>.
    /// </remarks>
    /// <param name="amount">The amount as typed, with the decimal places it was written with.</param>
    /// <param name="currency">The currency.</param>
    /// <param name="field">The field name used in the validation error.</param>
    /// <param name="label">The label used in the validation message, for example <c>Base price</c>.</param>
    /// <returns>
    /// The money value, or a validation error: <c>Currency is required.</c> on <c>currency</c> when the currency is missing; otherwise, on
    /// <paramref name="field"/>, <c>{label} must be a whole number, like 1500.</c> for a currency without decimals and
    /// <c>{label} must have at most 2 decimal places, like 1500 or 1500.50.</c> for one with two, the example having as many decimal places
    /// as the currency.
    /// </returns>
    public static Result<Money> CreateFromInput(decimal amount, Currency? currency, string field = "amount", string label = "Amount")
    {
        if (currency is not null && amount.Scale > currency.MinorUnits)
        {
            var message = currency.MinorUnits == 0
                ? $"{label} must be a whole number, like 1500."
                : $"{label} must have at most {currency.MinorUnits} decimal places, like 1500 or 1500.5{new string('0', currency.MinorUnits - 1)}.";
            return Result.Invalid<Money>(detail: message, field: field, message: message);
        }

        return CreateExact(amount, currency, field, label);
    }

    /// <summary>For persistence mappers: rebuilds a value from a stored amount and currency; the amount is NOT rounded and the currency is not checked against the allow-list, so a stored value that a newer rule would reject can still be loaded; never use it for user input.</summary>
    /// <param name="amount">The stored amount.</param>
    /// <param name="currency">The stored currency, typically from <see cref="Currency.Restore"/>.</param>
    /// <returns>The money value.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="currency"/> is <see langword="null"/>.</exception>
    public static Money Restore(decimal amount, Currency currency)
    {
        ArgumentNullException.ThrowIfNull(currency);
        return new Money(amount, currency);
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

    /// <summary>
    /// Formats the money for people: the culture's currency pattern, separators and negative form, with <see cref="Currency.Symbol"/> as the
    /// symbol and the currency's minor unit as the decimals, for example <c>R$ 1.234,56</c> (pt-BR, BRL), <c>€ 1.234,56</c> (pt-BR, EUR),
    /// <c>1 234,56 €</c> (pt-PT, EUR) or <c>$1,234.56</c> (en-US, USD). The pattern comes from the platform's culture data (ICU on Linux
    /// and macOS); in a host with invariant globalization every culture formats like the invariant culture, <c>€1,234.56</c>.
    /// </summary>
    /// <param name="provider">The culture, such as <see cref="CultureInfo.CurrentCulture"/>, or another format provider.</param>
    /// <returns>The display text.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="provider"/> is <see langword="null"/>.</exception>
    public string ToDisplayString(IFormatProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        return ToString(DisplayFormat, provider);
    }

    /// <summary>
    /// Formats the money for people like <see cref="ToDisplayString(IFormatProvider)"/>, with the options' departures from the culture's
    /// format: the symbol before or after the amount (keeping whether the culture puts a space between them), and a whole amount without
    /// its zero minor units. In pt-BR, EUR 2890.00 with <see cref="MoneySymbolPlacement.After"/> and
    /// <see cref="MoneyDisplayOptions.OmitZeroMinorUnits"/> gives <c>2.890 €</c>, and EUR 2890.50 gives <c>2.890,50 €</c>.
    /// </summary>
    /// <param name="provider">The culture, such as <see cref="CultureInfo.CurrentCulture"/>, or another format provider.</param>
    /// <param name="options">The display options.</param>
    /// <returns>The display text.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public string ToDisplayString(IFormatProvider provider, MoneyDisplayOptions options)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(options);

        var numberFormat = DisplayFormatOf(provider);
        if (options.OmitZeroMinorUnits && Amount == decimal.Truncate(Amount))
        {
            numberFormat.CurrencyDecimalDigits = 0;
        }

        if (options.SymbolPlacement != MoneySymbolPlacement.Culture)
        {
            var spaced = numberFormat.CurrencyPositivePattern is 2 or 3;
            var before = options.SymbolPlacement == MoneySymbolPlacement.Before;
            numberFormat.CurrencyPositivePattern = (before, spaced) switch
            {
                (true, false) => 0,
                (false, false) => 1,
                (true, true) => 2,
                _ => 3,
            };
            numberFormat.CurrencyNegativePattern = (before, spaced) switch
            {
                (true, false) => 1,
                (false, false) => 5,
                (true, true) => 9,
                _ => 8,
            };
        }

        return Amount.ToString(DisplayFormat, numberFormat);
    }

    /// <summary>
    /// Formats the money: <c>C</c> as <see cref="ToDisplayString(IFormatProvider)"/> does, and <c>G</c>, an empty or a <see langword="null"/> format as the
    /// invariant <see cref="ToString()"/>, whatever the provider.
    /// </summary>
    /// <param name="format">The format: <c>C</c>, <c>G</c>, empty or <see langword="null"/>.</param>
    /// <param name="formatProvider">The culture for <c>C</c>; the current culture when <see langword="null"/>.</param>
    /// <returns>The formatted text.</returns>
    /// <exception cref="FormatException"><paramref name="format"/> is not one of the supported formats.</exception>
    public string ToString(string? format, IFormatProvider? formatProvider)
    {
        if (string.IsNullOrEmpty(format) || format is "G" or "g")
        {
            return ToString();
        }

        if (format is not (DisplayFormat or "c"))
        {
            throw new FormatException($"The format '{format}' is not supported; use \"C\" for display or \"G\" for the invariant form.");
        }

        return Amount.ToString(DisplayFormat, DisplayFormatOf(formatProvider ?? CultureInfo.CurrentCulture));
    }

    private NumberFormatInfo DisplayFormatOf(IFormatProvider provider)
    {
        var numberFormat = (NumberFormatInfo)NumberFormatInfo.GetInstance(provider).Clone();
        numberFormat.CurrencySymbol = Currency.Symbol;
        numberFormat.CurrencyDecimalDigits = Currency.MinorUnits;
        return numberFormat;
    }

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Amount;
        yield return Currency;
    }
}
