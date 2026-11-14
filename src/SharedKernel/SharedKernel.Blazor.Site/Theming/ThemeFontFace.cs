using PandaBear.BuildingBlocks;
using PandaBear.Results;

namespace PandaBear.Blazor.Site.Theming;

/// <summary>One file of a self-hosted font: its weight, its style and a root-relative or https <c>.woff2</c> address.</summary>
public sealed class ThemeFontFace : ValueObject
{
    private ThemeFontFace(int weight, ThemeFontStyle style, LinkTarget src)
    {
        Weight = weight;
        Style = style;
        Src = src;
    }

    /// <summary>Gets the weight, a multiple of 100 from 100 to 900.</summary>
    public int Weight { get; }

    /// <summary>Gets the style.</summary>
    public ThemeFontStyle Style { get; }

    /// <summary>Gets the address of the <c>.woff2</c> file.</summary>
    public LinkTarget Src { get; }

    /// <summary>Creates a face.</summary>
    /// <param name="weight">The weight, a multiple of 100 from 100 to 900.</param>
    /// <param name="style">The style.</param>
    /// <param name="src">
    /// The file: root-relative or https (<see cref="LinkTarget.IsImageSource" />), with a path ending in <c>.woff2</c>, no fragment,
    /// and none of <c>" ' ( ) &lt; &gt;</c>, so it can be written into <c>url("…")</c> unchanged.
    /// </param>
    /// <returns>The face, or validation errors on the fields <c>weight</c>, <c>style</c> and <c>src</c>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="src" /> is <see langword="null" />.</exception>
    public static Result<ThemeFontFace> Create(int weight, ThemeFontStyle style, LinkTarget src)
    {
        ArgumentNullException.ThrowIfNull(src);
        var errors = new ThemeErrors();
        if (!ThemeFont.IsWeight(weight))
        {
            errors.Add("weight", ThemeFont.WeightMessage);
        }

        if (!Enum.IsDefined(style))
        {
            errors.Add("style", "The style must be normal or italic.");
        }

        if (!IsFontFile(src))
        {
            errors.Add("src", "A font file must be a root-relative or https address of a .woff2 file.");
        }

        return errors.HasErrors ? errors.ToResult<ThemeFontFace>("The font face is invalid.") : Result.Ok(new ThemeFontFace(weight, style, src));
    }

    /// <inheritdoc />
    public override string ToString() => $"{Weight} {Style} {Src}";

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Weight;
        yield return Style;
        yield return Src;
    }

    private static bool IsFontFile(LinkTarget src)
    {
        var value = src.Value;
        if (!src.IsImageSource || value.Contains('#', StringComparison.Ordinal) || value.Any(character => character is '"' or '\'' or '(' or ')' or '<' or '>' || !char.IsAscii(character)))
        {
            return false;
        }

        var queryStart = value.IndexOf('?', StringComparison.Ordinal);
        var path = queryStart < 0 ? value : value[..queryStart];
        return path.EndsWith(".woff2", StringComparison.OrdinalIgnoreCase);
    }
}
