using PandaBear.Blazor.Site.Theming;
using PandaBear.Results;

namespace PandaBear.Blazor.Site.Tests.Theming;

/// <summary>Builds the themes and fonts that the theme tests share.</summary>
internal static class ThemeFixtures
{
    /// <summary>Gets the validation errors of a typed result keyed by field path.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="result">The result.</param>
    /// <returns>The messages by field, or an empty dictionary.</returns>
    public static IReadOnlyDictionary<string, string[]> FieldErrors<T>(this Result<T> result)
        where T : notnull => result.WithoutValue().ValidationErrors();

    /// <summary>Creates a colour from an author-controlled literal.</summary>
    /// <param name="light">The light value.</param>
    /// <param name="dark">The dark value.</param>
    /// <returns>The colour pair.</returns>
    public static ThemeColor Pair(string light, string dark) => ThemeColor.Create(CssColor.FromString(light), CssColor.FromString(dark));

    /// <summary>Creates a font with one regular face at <c>/fonts/{key}.woff2</c>.</summary>
    /// <param name="key">The key.</param>
    /// <param name="family">The family name.</param>
    /// <returns>The font.</returns>
    public static ThemeFont Font(string key, string family) => ThemeFont.Create(
        key,
        family,
        [ThemeFontFace.Create(400, ThemeFontStyle.Normal, LinkTarget.FromString($"/fonts/{key}.woff2")).ShouldBeSuccess()]).ShouldBeSuccess();

    /// <summary>Creates an upright face at <c>/fonts/inter-{weight}.woff2</c>.</summary>
    /// <param name="weight">The weight.</param>
    /// <returns>The face.</returns>
    public static ThemeFontFace Face(int weight) =>
        ThemeFontFace.Create(weight, ThemeFontStyle.Normal, LinkTarget.FromString($"/fonts/inter-{weight}.woff2")).ShouldBeSuccess();

    /// <summary>Creates a style with a size and nothing else.</summary>
    /// <param name="min">The minimum size in rem.</param>
    /// <param name="max">The maximum size in rem; the same as the minimum when omitted.</param>
    /// <returns>The style.</returns>
    public static TypographyStyle Sized(decimal min, decimal? max = null) => TypographyStyle.Create(
        size: max is null || max == min ? FontSize.Fixed(min).ShouldBeSuccess() : FontSize.Fluid(min, max.Value).ShouldBeSuccess()).ShouldBeSuccess();

    /// <summary>Replaces one colour of the default theme.</summary>
    /// <param name="role">The role.</param>
    /// <param name="color">The new colour.</param>
    /// <returns>The colours.</returns>
    public static SiteColors DefaultColorsWith(ThemeColorRole role, ThemeColor color) => SiteTheme.Default.Colors.With(role, color);

    /// <summary>Replaces one typography role of the default theme.</summary>
    /// <param name="role">The role.</param>
    /// <param name="style">The new style.</param>
    /// <returns>The typography.</returns>
    public static SiteTypography DefaultTypographyWith(TypographyRole role, TypographyStyle style) => SiteTheme.Default.Typography.With(role, style);
}
