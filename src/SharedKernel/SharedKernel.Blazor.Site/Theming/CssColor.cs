using System.Globalization;
using SharedKernel.BuildingBlocks;
using SharedKernel.Results;

namespace SharedKernel.Blazor.Site.Theming;

/// <summary>
/// An opaque sRGB colour written as <c>#rrggbb</c>, with the WCAG 2.2 relative luminance and contrast ratio. Only hex is accepted,
/// so a colour can be written into a stylesheet as is and its contrast can always be computed.
/// </summary>
public sealed class CssColor : ValueObject
{
    private const string Field = "color";

    private CssColor(string value, byte red, byte green, byte blue)
    {
        Value = value;
        Red = red;
        Green = green;
        Blue = blue;
        RelativeLuminance = (0.2126 * Linear(red)) + (0.7152 * Linear(green)) + (0.0722 * Linear(blue));
    }

    /// <summary>Gets the colour as lower-case <c>#rrggbb</c>.</summary>
    public string Value { get; }

    /// <summary>Gets the WCAG 2.2 relative luminance, from 0 (black) to 1 (white).</summary>
    public double RelativeLuminance { get; }

    private byte Red { get; }

    private byte Green { get; }

    private byte Blue { get; }

    /// <summary>Creates a colour from <c>#rgb</c> or <c>#rrggbb</c> in any letter case; surrounding white space is ignored.</summary>
    /// <param name="value">The candidate colour.</param>
    /// <returns>The colour in lower-case <c>#rrggbb</c>, or an input-independent validation error on the field <c>color</c>.</returns>
    public static Result<CssColor> Create(string? value)
    {
        var text = value?.Trim();
        if (text is null || (text.Length != 4 && text.Length != 7) || text[0] != '#' || !text.Skip(1).All(char.IsAsciiHexDigit))
        {
            return ThemeErrors.Invalid<CssColor>(Field, "A colour must be written as #rrggbb or #rgb.");
        }

        var hex = text.Length == 4
            ? string.Concat(text[1], text[1], text[2], text[2], text[3], text[3])
            : text[1..];
        return Result.Ok(FromChannels(
            byte.Parse(hex.AsSpan(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            byte.Parse(hex.AsSpan(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            byte.Parse(hex.AsSpan(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture)));
    }

    /// <summary>Creates a colour from text that the author controls, such as a literal default.</summary>
    /// <param name="value">The colour.</param>
    /// <returns>The colour.</returns>
    /// <exception cref="ArgumentException"><paramref name="value" /> is not a hex colour.</exception>
    public static CssColor FromString(string value)
    {
        var created = Create(value);
        return created.TryGetValue(out var color) ? color : throw new ArgumentException(created.ErrorDetails?.Detail, nameof(value));
    }

    /// <summary>
    /// Computes the WCAG 2.2 contrast ratio with another colour: <c>(L1 + 0.05) / (L2 + 0.05)</c>, the lighter luminance first. The
    /// order of the two colours does not matter. The ratio is not rounded, so 4.499 does not reach 4.5.
    /// </summary>
    /// <param name="other">The other colour.</param>
    /// <returns>The ratio, from 1 to 21.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="other" /> is <see langword="null" />.</exception>
    public double ContrastRatio(CssColor other)
    {
        ArgumentNullException.ThrowIfNull(other);
        var lighter = Math.Max(RelativeLuminance, other.RelativeLuminance);
        var darker = Math.Min(RelativeLuminance, other.RelativeLuminance);
        return (lighter + 0.05) / (darker + 0.05);
    }

    /// <inheritdoc />
    public override string ToString() => Value;

    /// <summary>Mixes this colour with another in sRGB channel space.</summary>
    /// <param name="other">The colour to mix in.</param>
    /// <param name="weight">The share of <paramref name="other" />, from 0 to 1.</param>
    /// <returns>The mixed colour.</returns>
    internal CssColor Mix(CssColor other, double weight) => FromChannels(
        MixChannel(Red, other.Red, weight),
        MixChannel(Green, other.Green, weight),
        MixChannel(Blue, other.Blue, weight));

    /// <summary>
    /// Lays this colour at an opacity over a photo of one plain colour, as <c>color-mix(in srgb, this p%, transparent)</c> composited
    /// in sRGB. Each channel is rounded towards the photo, up over white and down over black, so a browser's own rounding never
    /// gives a lower contrast than the result.
    /// </summary>
    /// <param name="percent">The opacity, from 0 to 100.</param>
    /// <param name="overWhite">Whether the photo is white; otherwise it is black.</param>
    /// <returns>The composited colour.</returns>
    internal CssColor Over(int percent, bool overWhite)
    {
        var weight = percent / 100.0;
        var photo = overWhite ? 255.0 : 0.0;
        return FromChannels(Composite(Red, weight, photo, overWhite), Composite(Green, weight, photo, overWhite), Composite(Blue, weight, photo, overWhite));
    }

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    private static CssColor FromChannels(byte red, byte green, byte blue) =>
        new(string.Create(CultureInfo.InvariantCulture, $"#{red:x2}{green:x2}{blue:x2}"), red, green, blue);

    private static byte MixChannel(byte from, byte to, double weight) =>
        (byte)Math.Round(from + ((to - from) * weight), MidpointRounding.AwayFromZero);

    private static byte Composite(byte channel, double weight, double photo, bool roundUp)
    {
        var value = Math.Round((channel * weight) + (photo * (1 - weight)), 9);
        return (byte)(roundUp ? Math.Ceiling(value) : Math.Floor(value));
    }

    // WCAG 2.2 relative luminance (https://www.w3.org/TR/WCAG22/#dfn-relative-luminance) with the sRGB threshold 0.04045;
    // the older 0.03928 gives the same result for every 8-bit channel value.
    private static double Linear(byte channel)
    {
        var value = channel / 255.0;
        return value <= 0.04045 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
    }
}
