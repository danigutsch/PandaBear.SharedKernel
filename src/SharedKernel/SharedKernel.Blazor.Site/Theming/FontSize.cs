using PandaBear.BuildingBlocks;
using PandaBear.Results;

namespace PandaBear.Blazor.Site.Theming;

/// <summary>
/// A font size in <c>rem</c>: fixed, or fluid from <see cref="Min" /> at a <see cref="FluidFromWidth" /> wide viewport to
/// <see cref="Max" /> at <see cref="FluidToWidth" />, rendered as <c>clamp(min, intercept + slope·vw, max)</c>. The preferred value
/// keeps a <c>rem</c> part, so browser zoom and the user's font size still scale the text.
/// </summary>
public sealed class FontSize : ValueObject
{
    /// <summary>The smallest accepted size, in rem.</summary>
    public const decimal Smallest = 0.5m;

    /// <summary>The largest accepted size, in rem.</summary>
    public const decimal Largest = 10m;

    /// <summary>The viewport width, in rem, at which a fluid size reaches its minimum (320 px at 16 px per rem).</summary>
    public const decimal FluidFromWidth = 20m;

    /// <summary>The viewport width, in rem, at which a fluid size reaches its maximum (1280 px at 16 px per rem).</summary>
    public const decimal FluidToWidth = 80m;

    private const int MaxDecimals = 4;
    private const string Field = "size";

    private FontSize(decimal min, decimal max)
    {
        Min = min;
        Max = max;
    }

    /// <summary>Gets the minimum size in rem; for a fixed size, the size.</summary>
    public decimal Min { get; }

    /// <summary>Gets the maximum size in rem; for a fixed size, the size.</summary>
    public decimal Max { get; }

    /// <summary>Gets a value indicating whether the size grows with the viewport.</summary>
    public bool IsFluid => Min != Max;

    /// <summary>Creates a fixed size.</summary>
    /// <param name="rem">The size in rem, from <see cref="Smallest" /> to <see cref="Largest" /> with at most four decimals.</param>
    /// <returns>The size, or a validation error on the field <c>size</c>.</returns>
    public static Result<FontSize> Fixed(decimal rem) =>
        IsInRange(rem) ? Result.Ok(new FontSize(rem, rem)) : OutOfRange();

    /// <summary>Creates a fluid size.</summary>
    /// <param name="minRem">The size at narrow viewports.</param>
    /// <param name="maxRem">The size at wide viewports; larger than <paramref name="minRem" />.</param>
    /// <returns>The size, or a validation error on the field <c>size</c>.</returns>
    public static Result<FontSize> Fluid(decimal minRem, decimal maxRem)
    {
        if (!IsInRange(minRem) || !IsInRange(maxRem))
        {
            return OutOfRange();
        }

        return minRem < maxRem
            ? Result.Ok(new FontSize(minRem, maxRem))
            : ThemeErrors.Invalid<FontSize>(Field, "The largest size of a fluid size must be larger than its smallest size.");
    }

    /// <inheritdoc />
    public override string ToString() => ToCss("vw");

    /// <summary>Writes the size as a CSS length.</summary>
    /// <param name="widthUnit">The unit of 1% of the reference width: <c>vw</c> for the page, <c>cqi</c> inside a size container.</param>
    /// <returns>A <c>rem</c> length or a <c>clamp()</c>.</returns>
    internal string ToCss(string widthUnit)
    {
        if (!IsFluid)
        {
            return CssText.Number(Min) + "rem";
        }

        var slope = (Max - Min) / (FluidToWidth - FluidFromWidth);
        var intercept = CssText.Round(Min - (slope * FluidFromWidth));
        var perWidth = CssText.Number(slope * 100m) + widthUnit;
        var preferred = intercept switch
        {
            > 0 => $"{CssText.Number(intercept)}rem + {perWidth}",
            < 0 => $"{perWidth} - {CssText.Number(-intercept)}rem",
            _ => perWidth,
        };
        return $"clamp({CssText.Number(Min)}rem, {preferred}, {CssText.Number(Max)}rem)";
    }

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Min;
        yield return Max;
    }

    private static bool IsInRange(decimal rem) => rem >= Smallest && rem <= Largest && decimal.Round(rem, MaxDecimals) == rem;

    private static Result<FontSize> OutOfRange() => ThemeErrors.Invalid<FontSize>(
        Field,
        $"A size must be from {CssText.Number(Smallest)}rem to {CssText.Number(Largest)}rem with at most {MaxDecimals} decimals.");
}
