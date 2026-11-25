namespace SharedKernel.Blazor.Site.Theming;

/// <summary>The typography of one role in a <see cref="ThemeDocument" />; a missing value keeps <see cref="SiteTheme.Default" />'s.</summary>
/// <param name="FontKey">The key of a font of the theme.</param>
/// <param name="Fallback">The system stack written after the font.</param>
/// <param name="SizeMin">The size in rem, or the smallest size of a fluid size.</param>
/// <param name="SizeMax">The largest size of a fluid size in rem.</param>
/// <param name="Weight">The weight, a multiple of 100 from 100 to 900.</param>
/// <param name="LineHeight">The unitless line height.</param>
/// <param name="LetterSpacing">The letter spacing in em.</param>
/// <param name="Transform">The text transform.</param>
/// <param name="Italic">Whether the text is italic.</param>
/// <param name="Color">The colour of a heading role or the brand: the text, primary or muted colour.</param>
public sealed record TypographyDocument(
    string? FontKey = null,
    GenericFontFamily? Fallback = null,
    decimal? SizeMin = null,
    decimal? SizeMax = null,
    int? Weight = null,
    decimal? LineHeight = null,
    decimal? LetterSpacing = null,
    ThemeTextTransform? Transform = null,
    bool? Italic = null,
    ThemeColorRole? Color = null);
