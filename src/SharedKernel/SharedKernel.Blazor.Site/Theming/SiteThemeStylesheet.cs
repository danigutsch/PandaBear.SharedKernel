using System.Security.Cryptography;
using System.Text;

namespace SharedKernel.Blazor.Site.Theming;

/// <summary>
/// Renders a <see cref="SiteTheme" /> as a stylesheet that <c>site.css</c> consumes: <c>@font-face</c> rules with
/// <c>font-display: swap</c>, then <c>:root</c> custom properties, every colour as <c>light-dark()</c>, the photo scrim of
/// <see cref="SiteScrim" /> as percentages, the brand and header heights, the radii and the divider of the <see cref="SiteShape" />,
/// every set typography value as <c>--site-{role}-{property}</c> and the colour of every heading role that sets one. The output depends only on the theme, in a fixed order, so <see cref="Hash" /> can version
/// an immutable <c>/theme.css?v=…</c> address. Every value comes from a validated value object and strings are escaped as well.
/// </summary>
public static class SiteThemeStylesheet
{
    private const string SansSerifStack = "system-ui, -apple-system, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif";
    private const string SerifStack = "Georgia, \"Times New Roman\", Times, serif";
    private const string MonospaceStack = "ui-monospace, SFMono-Regular, Menlo, Consolas, \"Liberation Mono\", monospace";

    /// <summary>
    /// The padding of a button, computed from its own font size and line height: a block padding that makes the button 2.75 times
    /// its font size tall (at least a quarter of it), and an inline padding of half that height, so a pill's round end never reaches
    /// the text. <c>site.css</c> and the preview use the same value.
    /// </summary>
    internal const string ButtonPadding = "max(0.25em, (2.75em - 1lh) / 2) 1.375em";

    private const string RoundedRadius = "0.5rem";
    private const string PillRadius = "999rem";

    private static readonly string[] Properties = ["font-family", "font-size", "font-weight", "line-height", "letter-spacing", "text-transform", "font-style"];

    private static readonly TypographyRole[] HeadingRoles = [.. Enum.GetValues<TypographyRole>().Where(ThemeNames.IsHeading)];

    /// <summary>Gets the colour custom properties, in render order, such as <c>--site-color-surface-alt</c>.</summary>
    public static IReadOnlyList<string> ColorVariables { get; } =
        [.. Enum.GetValues<ThemeColorRole>().Select(role => "--site-color-" + ThemeNames.Css(role))];

    /// <summary>
    /// Gets the scrim custom properties, in render order: the opacities of <see cref="SiteScrim.Text" />,
    /// <see cref="SiteScrim.LargeText" /> and <see cref="SiteScrim.Boost" /> as percentages.
    /// </summary>
    public static IReadOnlyList<string> ScrimVariables { get; } = ["--site-scrim", "--site-scrim-large", "--site-scrim-boost"];

    /// <summary>
    /// Gets the header custom properties, in render order: <see cref="SiteTheme.BrandHeight" /> and
    /// <see cref="SiteTheme.HeaderHeight" /> in rem.
    /// </summary>
    public static IReadOnlyList<string> HeaderVariables { get; } = ["--site-brand-height", "--site-header-height"];

    /// <summary>
    /// Gets the shape custom properties, in render order: the surface radius (<c>--site-radius</c>) and the control radius of
    /// <see cref="SiteShape.Control" />, then the divider's width and thickness in rem and its colour.
    /// </summary>
    public static IReadOnlyList<string> ShapeVariables { get; } =
        ["--site-radius", "--site-control-radius", "--site-divider-width", "--site-divider-thickness", "--site-divider-color"];

    /// <summary>
    /// Gets the colour custom properties of the heading roles (h1 to h6 and the card title), in render order, such as
    /// <c>--site-h2-color</c>; one is rendered only when its role sets a <see cref="TypographyStyle.Color" />. The brand takes a colour
    /// too, <c>--site-brand-color</c>, but it is not a heading, keeps the primary colour without one, and is not listed here.
    /// </summary>
    public static IReadOnlyList<string> HeadingColorVariables { get; } = [.. HeadingRoles.Select(role => Variable(role, "color"))];

    /// <summary>Gets the per-role custom properties, in render order, such as <c>--site-h1-font-size</c>.</summary>
    public static IReadOnlyList<string> RoleVariables { get; } =
        [.. from role in Enum.GetValues<TypographyRole>() from property in Properties select Variable(role, property)];

    /// <summary>Renders the stylesheet of a theme for the whole page (<c>:root</c>).</summary>
    /// <param name="theme">The theme.</param>
    /// <returns>The CSS.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="theme" /> is <see langword="null" />.</exception>
    public static string Render(SiteTheme theme)
    {
        ArgumentNullException.ThrowIfNull(theme);
        var css = new StringBuilder();
        AppendFontFaces(css, theme);
        css.Append(":root {\n");
        AppendVariables(css, theme, "vw");
        css.Append("}\n");
        return css.ToString();
    }

    /// <summary>
    /// Computes the content hash of <see cref="Render" />: the first eight bytes of its UTF-8 SHA-256 as 16 lower-case hex digits.
    /// </summary>
    /// <param name="theme">The theme.</param>
    /// <returns>The hash.</returns>
    public static string Hash(SiteTheme theme) => RenderVersioned(theme).Version;

    /// <summary>Renders the CSS once and computes its existing UTF-8 content hash.</summary>
    /// <param name="theme">The theme.</param>
    /// <returns>The CSS and its version, suitable for serving together.</returns>
    /// <exception cref="ArgumentNullException">The theme is null.</exception>
    public static (string Css, string Version) RenderVersioned(SiteTheme theme)
    {
        var css = Render(theme);
        var version = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(css)).AsSpan(0, 8));
        return (css, version);
    }

    /// <summary>
    /// Renders the stylesheet of a theme scoped to one element (the preview): the custom properties on <c>#scopeId</c>, the colour
    /// scheme from its <c>data-theme</c> attribute, fluid sizes against its width (<c>cqi</c>), the rules that apply the roles
    /// to the sample (the heading roles by the <c>site-theme-preview__h1</c> to <c>__h6</c> classes, not by heading elements) and the colours of the contrast swatches by their <c>data-foreground</c> and <c>data-background</c> roles, so the
    /// preview needs neither <c>site.css</c>, script nor style attributes.
    /// </summary>
    /// <param name="theme">The theme.</param>
    /// <param name="scopeId">The element id: a letter, then letters, digits, hyphens or underscores.</param>
    /// <returns>The CSS.</returns>
    /// <exception cref="ArgumentException"><paramref name="scopeId" /> is not a safe id.</exception>
    internal static string RenderScoped(SiteTheme theme, string scopeId)
    {
        ArgumentNullException.ThrowIfNull(theme);
        RequireScopeId(scopeId);
        var scope = "#" + scopeId;
        var sample = scope + " .site-theme-preview__sample";
        var css = new StringBuilder();
        AppendFontFaces(css, theme);
        css.Append(scope).Append(" {\n    color-scheme: light dark;\n    container-type: inline-size;\n");
        AppendVariables(css, theme, "cqi");
        css.Append("}\n");
        css.Append(scope).Append("[data-theme=\"light\"] {\n    color-scheme: light;\n}\n");
        css.Append(scope).Append("[data-theme=\"dark\"] {\n    color-scheme: dark;\n}\n");
        css.Append(scope).Append("[data-width=\"narrow\"] {\n    max-width: 22rem;\n}\n");
        AppendRule(css, sample, TypographyRole.Body, "padding: 1rem; background: var(--site-color-surface); color: var(--site-color-text);");
        var headings = new List<string>();
        for (var heading = TypographyRole.H1; heading <= TypographyRole.H6; heading++)
        {
            headings.Add(".site-theme-preview__" + ThemeNames.Css(heading));
            AppendRule(css, sample + " " + headings[^1], heading, "margin: 0 0 0.5rem;");
        }

        AppendRule(css, sample + " .site-theme-preview__card-title", TypographyRole.CardTitle, "margin: 0 0 0.5rem;");

        AppendRule(css, sample + " small", TypographyRole.Small, string.Empty);
        AppendRule(css, sample + " a", TypographyRole.Link, "color: var(--site-color-primary);");
        css.Append(sample).Append(" :is(").AppendJoin(", ", headings).Append(") a {\n    font: inherit;\n    letter-spacing: inherit;\n    text-transform: inherit;\n}\n");
        AppendRule(css, sample + " .site-button", TypographyRole.Button, "display: inline-flex; align-items: center; justify-content: center; min-block-size: 2.75rem; margin: 0 0.5rem 0.5rem 0; padding: " + ButtonPadding + "; border: 2px solid transparent; border-radius: var(--site-control-radius); text-decoration: none;");
        css.Append(sample).Append(" .site-nav {\n    display: flex;\n    align-items: center;\n    gap: 1rem;\n}\n");
        css.Append(scope).Append(" .site-theme-preview__brand {\n    display: inline-flex;\n    align-items: center;\n    height: var(--site-brand-height);\n    padding: 0 0.5rem;\n    border: 1px dashed currentColor;\n}\n");
        AppendRule(css, sample + " .site-theme-preview__brand", TypographyRole.Brand, string.Empty);
        AppendRule(css, sample + " .site-nav a", TypographyRole.Nav, "color: var(--site-color-text); text-decoration: none;");
        css.Append(sample).Append(" .site-button--primary {\n    background: var(--site-color-primary);\n    color: var(--site-color-on-primary);\n}\n");
        css.Append(sample).Append(" .site-button--secondary {\n    background: var(--site-color-accent);\n    color: var(--site-color-on-accent);\n}\n");
        css.Append(sample).Append(" .site-nav ul {\n    display: flex;\n    gap: 1rem;\n    margin: 0 0 1rem;\n    padding: 0 0 0.5rem;\n    list-style: none;\n    border-bottom: 1px solid var(--site-color-border);\n}\n");
        css.Append(sample).Append(" .site-nav a[aria-current=\"page\"] {\n    color: var(--site-color-primary);\n    text-decoration: underline;\n}\n");
        css.Append(sample).Append(" .site-card {\n    margin-top: 0.5rem;\n    padding: 1rem;\n    background: var(--site-color-surface-alt);\n    border: 1px solid var(--site-color-border);\n    border-radius: var(--site-radius);\n}\n");
        css.Append(sample).Append(" .site-card p {\n    margin: 0;\n    color: var(--site-color-muted);\n}\n");
        css.Append(scope).Append(" .site-theme-preview__swatch {\n    display: inline-block;\n    min-width: 2.5em;\n    margin-inline-end: 0.5em;\n    padding: 0 0.25em;\n    border: 1px solid currentColor;\n    text-align: center;\n}\n");

        // The swatches take their colours from these rules, not from style attributes, which a policy without 'unsafe-inline' blocks.
        foreach (var role in Enum.GetValues<ThemeColorRole>())
        {
            var name = ThemeNames.Css(role);
            css.Append(scope).Append(" .site-theme-preview__swatch[data-foreground=\"").Append(name).Append("\"] {\n    color: var(--site-color-").Append(name).Append(");\n}\n");
            css.Append(scope).Append(" .site-theme-preview__swatch[data-background=\"").Append(name).Append("\"] {\n    background: var(--site-color-").Append(name).Append(");\n}\n");
        }

        return css.ToString();
    }

    /// <summary>Checks an element id that is written into a selector.</summary>
    /// <param name="scopeId">The id.</param>
    /// <exception cref="ArgumentException">The id is empty, too long, does not start with a letter or has other characters than letters, digits, hyphens and underscores.</exception>
    internal static void RequireScopeId(string scopeId)
    {
        if (string.IsNullOrEmpty(scopeId)
            || scopeId.Length > 64
            || !char.IsAsciiLetter(scopeId[0])
            || !scopeId.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_'))
        {
            throw new ArgumentException("A preview id must start with a letter and contain only letters, digits, hyphens and underscores.", nameof(scopeId));
        }
    }

    private static string Variable(TypographyRole role, string property) => $"--site-{ThemeNames.Css(role)}-{property}";

    private static void AppendFontFaces(StringBuilder css, SiteTheme theme)
    {
        foreach (var font in theme.Fonts)
        {
            foreach (var face in font.Faces)
            {
                css.Append("@font-face {\n    font-family: ").Append(CssText.Quoted(font.Family)).Append(";\n")
                    .Append("    src: url(").Append(CssText.Quoted(face.Src.Value)).Append(") format(\"woff2\");\n")
                    .Append("    font-weight: ").Append(face.Weight).Append(";\n")
                    .Append("    font-style: ").Append(face.Style == ThemeFontStyle.Italic ? "italic" : "normal").Append(";\n")
                    .Append("    font-display: swap;\n}\n");
            }
        }
    }

    private static void AppendVariables(StringBuilder css, SiteTheme theme, string widthUnit)
    {
        foreach (var role in Enum.GetValues<ThemeColorRole>())
        {
            var color = theme.Colors[role];
            css.Append("    --site-color-").Append(ThemeNames.Css(role)).Append(": light-dark(")
                .Append(color.Light.Value).Append(", ").Append(color.Dark.Value).Append(");\n");
        }

        var scrim = SiteScrim.For(theme.Colors);
        int[] percents = [scrim.Text, scrim.LargeText, scrim.Boost];
        for (var index = 0; index < percents.Length; index++)
        {
            css.Append("    ").Append(ScrimVariables[index]).Append(": ")
                .Append(percents[index].ToString(System.Globalization.CultureInfo.InvariantCulture)).Append("%;\n");
        }

        css.Append("    ").Append(HeaderVariables[0]).Append(": ").Append(CssText.Number(theme.BrandHeight)).Append("rem;\n");
        css.Append("    ").Append(HeaderVariables[1]).Append(": ").Append(CssText.Number(theme.HeaderHeight)).Append("rem;\n");

        var shape = theme.Shape;
        string[] shapeValues =
        [
            shape.Control == ControlShape.Square ? "0" : RoundedRadius,
            shape.Control switch { ControlShape.Square => "0", ControlShape.Pill => PillRadius, _ => RoundedRadius },
            CssText.Number(shape.DividerWidth) + "rem",
            CssText.Number(shape.DividerThickness) + "rem",
            "var(--site-color-" + ThemeNames.Css(shape.DividerColor) + ")",
        ];
        for (var index = 0; index < shapeValues.Length; index++)
        {
            css.Append("    ").Append(ShapeVariables[index]).Append(": ").Append(shapeValues[index]).Append(";\n");
        }

        foreach (var role in Enum.GetValues<TypographyRole>())
        {
            var style = theme.Effective(role);
            foreach (var (property, value) in Values(theme, style, widthUnit))
            {
                css.Append("    ").Append(Variable(role, property)).Append(": ").Append(value).Append(";\n");
            }

            if (ThemeNames.TakesColor(role) && style.Color is { } color)
            {
                css.Append("    ").Append(Variable(role, "color")).Append(": var(--site-color-").Append(ThemeNames.Css(color)).Append(");\n");
            }
        }
    }

    private static IEnumerable<(string Property, string Value)> Values(SiteTheme theme, TypographyStyle style, string widthUnit)
    {
        if (style.SetsFamily)
        {
            var stack = style.Fallback switch
            {
                GenericFontFamily.Serif => SerifStack,
                GenericFontFamily.Monospace => MonospaceStack,
                _ => SansSerifStack,
            };
            var font = style.FontKey is null ? null : theme.Font(style.FontKey);
            yield return ("font-family", font is null ? stack : CssText.Quoted(font.Family) + ", " + stack);
        }

        if (style.Size is { } size)
        {
            yield return ("font-size", size.ToCss(widthUnit));
        }

        if (style.Weight is { } weight)
        {
            yield return ("font-weight", weight.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        if (style.LineHeight is { } lineHeight)
        {
            yield return ("line-height", CssText.Number(lineHeight));
        }

        if (style.LetterSpacing is { } spacing)
        {
            yield return ("letter-spacing", CssText.Number(spacing) + "em");
        }

        if (style.Transform is { } transform)
        {
            yield return ("text-transform", transform switch
            {
                ThemeTextTransform.Uppercase => "uppercase",
                ThemeTextTransform.Capitalize => "capitalize",
                _ => "none",
            });
        }

        if (style.Italic is { } italic)
        {
            yield return ("font-style", italic ? "italic" : "normal");
        }
    }

    private static void AppendRule(StringBuilder css, string selector, TypographyRole role, string extra)
    {
        css.Append(selector).Append(" {\n");
        foreach (var property in Properties)
        {
            css.Append("    ").Append(property).Append(": var(").Append(Variable(role, property)).Append(", inherit);\n");
        }

        if (ThemeNames.TakesColor(role))
        {
            css.Append("    color: var(").Append(Variable(role, "color")).Append(", ").Append(ThemeNames.ColorFallback(role)).Append(");\n");
        }

        foreach (var declaration in extra.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            css.Append("    ").Append(declaration).Append(";\n");
        }

        css.Append("}\n");
    }
}
