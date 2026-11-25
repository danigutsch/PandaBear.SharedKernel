namespace SharedKernel.BuildingBlocks;

/// <summary>
/// How <see cref="Money.ToDisplayString(IFormatProvider, MoneyDisplayOptions)"/> departs from the culture's currency format, for example
/// <c>2.890 €</c> in pt-BR with <see cref="MoneySymbolPlacement.After"/> and <see cref="OmitZeroMinorUnits"/>. The default options change
/// nothing.
/// </summary>
public sealed record MoneyDisplayOptions
{
    private readonly MoneySymbolPlacement _symbolPlacement;

    /// <summary>Gets a value indicating whether a whole amount is shown without its zero minor units (<c>2.890 €</c>, not <c>2.890,00 €</c>).</summary>
    public bool OmitZeroMinorUnits { get; init; }

    /// <summary>Gets where the currency symbol goes. The default is <see cref="MoneySymbolPlacement.Culture"/>.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not a defined <see cref="MoneySymbolPlacement"/>.</exception>
    public MoneySymbolPlacement SymbolPlacement
    {
        get => _symbolPlacement;
        init
        {
            if (!Enum.IsDefined(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "The symbol placement is not defined.");
            }

            _symbolPlacement = value;
        }
    }
}
