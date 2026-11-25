namespace SharedKernel.Blazor.Tests;

/// <summary>A form model with whole, decimal and optional numbers, for the number text input tests.</summary>
internal sealed class NumberFormModel
{
    /// <summary>Gets or sets a required whole number.</summary>
    public int Quantity { get; set; }

    /// <summary>Gets or sets an optional whole number.</summary>
    public int? Rank { get; set; }

    /// <summary>Gets or sets a required decimal.</summary>
    public decimal Price { get; set; }

    /// <summary>Gets or sets an optional decimal.</summary>
    public decimal? Distance { get; set; }

    /// <summary>Gets or sets a text, which the number input does not support.</summary>
    public string Text { get; set; } = string.Empty;
}
