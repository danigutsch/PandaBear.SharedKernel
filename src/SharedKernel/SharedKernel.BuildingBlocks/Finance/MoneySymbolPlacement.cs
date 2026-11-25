namespace SharedKernel.BuildingBlocks;

/// <summary>Where <see cref="Money.ToDisplayString(IFormatProvider, MoneyDisplayOptions)"/> puts the currency symbol.</summary>
public enum MoneySymbolPlacement
{
    /// <summary>Where the culture's currency pattern puts it.</summary>
    Culture = 0,

    /// <summary>Before the amount, keeping whether the culture separates symbol and amount with a space.</summary>
    Before = 1,

    /// <summary>After the amount, keeping whether the culture separates symbol and amount with a space.</summary>
    After = 2,
}
