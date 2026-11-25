using SharedKernel.BuildingBlocks;
using SharedKernel.Results;

namespace SharedKernel.Blazor.Site.Theming;

/// <summary>
/// The typography of one role. Every value is optional: <see langword="null" /> keeps the value of <see cref="SiteTheme.Default" />
/// for that role, and where the default has none (links, buttons and navigation) the text inherits from its surroundings, so a
/// link in small print stays small.
/// </summary>
public sealed class TypographyStyle : ValueObject
{
    /// <summary>The smallest accepted unitless line height.</summary>
    public const decimal MinLineHeight = 0.8m;

    /// <summary>The largest accepted unitless line height.</summary>
    public const decimal MaxLineHeight = 3m;

    /// <summary>The smallest accepted letter spacing, in em.</summary>
    public const decimal MinLetterSpacing = -0.1m;

    /// <summary>The largest accepted letter spacing, in em.</summary>
    public const decimal MaxLetterSpacing = 0.5m;

    private TypographyStyle(
        string? fontKey,
        GenericFontFamily? fallback,
        FontSize? size,
        int? weight,
        decimal? lineHeight,
        decimal? letterSpacing,
        ThemeTextTransform? transform,
        bool? italic,
        ThemeColorRole? color)
    {
        FontKey = fontKey;
        Fallback = fallback;
        Size = size;
        Weight = weight;
        LineHeight = lineHeight;
        LetterSpacing = letterSpacing;
        Transform = transform;
        Italic = italic;
        Color = color;
    }

    /// <summary>Gets the style that sets nothing.</summary>
    public static TypographyStyle Inherit { get; } = new(null, null, null, null, null, null, null, null, null);

    /// <summary>Gets the key of the self-hosted <see cref="ThemeFont" />, or <see langword="null" /> for the system stack.</summary>
    public string? FontKey { get; }

    /// <summary>Gets the system stack after the self-hosted font, or the whole family without one.</summary>
    public GenericFontFamily? Fallback { get; }

    /// <summary>Gets the font size.</summary>
    public FontSize? Size { get; }

    /// <summary>Gets the font weight, a multiple of 100 from 100 to 900.</summary>
    public int? Weight { get; }

    /// <summary>Gets the unitless line height.</summary>
    public decimal? LineHeight { get; }

    /// <summary>Gets the letter spacing in em.</summary>
    public decimal? LetterSpacing { get; }

    /// <summary>Gets the text transform.</summary>
    public ThemeTextTransform? Transform { get; }

    /// <summary>Gets a value indicating whether the text is italic.</summary>
    public bool? Italic { get; }

    /// <summary>
    /// Gets the colour of a role that takes one (h1 to h6, the card title and the brand): the text, primary or muted colour, whose
    /// contrast on the surfaces the theme checks. <see langword="null" /> keeps the colour of the surroundings, or the primary colour
    /// for the brand, which is a link.
    /// </summary>
    public ThemeColorRole? Color { get; }

    /// <summary>Gets a value indicating whether the style sets a font family (a font key or a fallback).</summary>
    public bool SetsFamily => FontKey is not null || Fallback is not null;

    /// <summary>Creates a style; omitted values are not set.</summary>
    /// <param name="fontKey">The key of a <see cref="ThemeFont" />: a lower-case letter, then lower-case letters, digits or hyphens.</param>
    /// <param name="fallback">The system stack.</param>
    /// <param name="size">The size.</param>
    /// <param name="weight">The weight, a multiple of 100 from 100 to 900.</param>
    /// <param name="lineHeight">The unitless line height, from 0.8 to 3.</param>
    /// <param name="letterSpacing">The letter spacing in em, from -0.1 to 0.5.</param>
    /// <param name="transform">The text transform.</param>
    /// <param name="italic">Whether the text is italic.</param>
    /// <param name="color">The colour of a heading role: <see cref="ThemeColorRole.Text" />, <see cref="ThemeColorRole.Primary" /> or <see cref="ThemeColorRole.Muted" />.</param>
    /// <returns>The style, or validation errors on the fields <c>fontKey</c>, <c>fallback</c>, <c>weight</c>, <c>lineHeight</c>,
    /// <c>letterSpacing</c>, <c>transform</c> and <c>color</c>.</returns>
    public static Result<TypographyStyle> Create(
        string? fontKey = null,
        GenericFontFamily? fallback = null,
        FontSize? size = null,
        int? weight = null,
        decimal? lineHeight = null,
        decimal? letterSpacing = null,
        ThemeTextTransform? transform = null,
        bool? italic = null,
        ThemeColorRole? color = null)
    {
        var errors = new ThemeErrors();
        if (fontKey is not null && !ThemeFont.IsKey(fontKey))
        {
            errors.Add("fontKey", ThemeFont.KeyMessage);
        }

        if (fallback is { } family && !Enum.IsDefined(family))
        {
            errors.Add("fallback", "The fallback must be sans-serif, serif or monospace.");
        }

        if (weight is { } value && !ThemeFont.IsWeight(value))
        {
            errors.Add("weight", ThemeFont.WeightMessage);
        }

        if (lineHeight is { } height && (height < MinLineHeight || height > MaxLineHeight))
        {
            errors.Add("lineHeight", $"The line height must be from {CssText.Number(MinLineHeight)} to {CssText.Number(MaxLineHeight)}.");
        }

        if (letterSpacing is { } spacing && (spacing < MinLetterSpacing || spacing > MaxLetterSpacing))
        {
            errors.Add("letterSpacing", $"The letter spacing must be from {CssText.Number(MinLetterSpacing)}em to {CssText.Number(MaxLetterSpacing)}em.");
        }

        if (transform is { } textTransform && !Enum.IsDefined(textTransform))
        {
            errors.Add("transform", "The text transform must be none, uppercase or capitalize.");
        }

        if (color is { } role && role is not (ThemeColorRole.Text or ThemeColorRole.Primary or ThemeColorRole.Muted))
        {
            errors.Add("color", "The colour must be the text, primary or muted colour, whose contrast the theme checks.");
        }

        return errors.HasErrors
            ? errors.ToResult<TypographyStyle>("The typography style is invalid.")
            : Result.Ok(new TypographyStyle(fontKey, fallback, size, weight, lineHeight is null ? null : CssText.Round(lineHeight.Value), letterSpacing is null ? null : CssText.Round(letterSpacing.Value), transform, italic, color));
    }

    /// <inheritdoc />
    public override string ToString() => $"{FontKey ?? Fallback?.ToString() ?? "inherit"} {Size} {Weight}";

    /// <summary>Returns the face of this style: its font family, weight and style, without its size, spacing, transform or colour.</summary>
    /// <returns>The face.</returns>
    internal TypographyStyle Face() => new(FontKey, Fallback, null, Weight, null, null, null, Italic, null);

    /// <summary>Returns this style with every unset value taken from another style.</summary>
    /// <param name="fallbackStyle">The style that fills the gaps.</param>
    /// <returns>The merged style.</returns>
    internal TypographyStyle Over(TypographyStyle fallbackStyle) => new(
        SetsFamily ? FontKey : fallbackStyle.FontKey,
        SetsFamily ? Fallback : fallbackStyle.Fallback,
        Size ?? fallbackStyle.Size,
        Weight ?? fallbackStyle.Weight,
        LineHeight ?? fallbackStyle.LineHeight,
        LetterSpacing ?? fallbackStyle.LetterSpacing,
        Transform ?? fallbackStyle.Transform,
        Italic ?? fallbackStyle.Italic,
        Color ?? fallbackStyle.Color);

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return FontKey;
        yield return Fallback;
        yield return Size;
        yield return Weight;
        yield return LineHeight;
        yield return LetterSpacing;
        yield return Transform;
        yield return Italic;
        yield return Color;
    }
}
