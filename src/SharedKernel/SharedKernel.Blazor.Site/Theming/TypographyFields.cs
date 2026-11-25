namespace SharedKernel.Blazor.Site.Theming;

/// <summary>The editor fields of one typography role; an empty field keeps the default theme's value.</summary>
public sealed class TypographyFields
{
    /// <summary>Gets or sets the key of a self-hosted font.</summary>
    public string? FontKey { get; set; }

    /// <summary>Gets or sets the system fallback stack.</summary>
    public GenericFontFamily? Fallback { get; set; }

    /// <summary>Gets or sets the size in rem, or the smallest size of a fluid size.</summary>
    public decimal? SizeMin { get; set; }

    /// <summary>Gets or sets the largest size of a fluid size in rem; empty or equal to <see cref="SizeMin" /> for a fixed size.</summary>
    public decimal? SizeMax { get; set; }

    /// <summary>Gets or sets the weight, a multiple of 100 from 100 to 900.</summary>
    public int? Weight { get; set; }

    /// <summary>Gets or sets the unitless line height.</summary>
    public decimal? LineHeight { get; set; }

    /// <summary>Gets or sets the letter spacing in em.</summary>
    public decimal? LetterSpacing { get; set; }

    /// <summary>Gets or sets the text transform.</summary>
    public ThemeTextTransform? Transform { get; set; }

    /// <summary>Gets or sets whether the text is italic.</summary>
    public bool? Italic { get; set; }

    /// <summary>Gets or sets the colour of a heading role: the text, primary or muted colour.</summary>
    public ThemeColorRole? Color { get; set; }
}
