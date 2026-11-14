using PandaBear.BuildingBlocks;
using PandaBear.Results;

namespace PandaBear.Blazor.Site.Theming;

/// <summary>
/// An owner-editable site theme: colour tokens with a light and a dark value, typography per role, and self-hosted fonts.
/// <see cref="Create" /> enforces every rule (see <see cref="Review" />); <see cref="CreateDraft" /> only enforces what rendering
/// needs (unique font keys and existing font references), so an editor can preview a theme that still breaks a rule.
/// </summary>
public sealed class SiteTheme : ValueObject
{
    private SiteTheme(SiteColors colors, SiteTypography typography, IReadOnlyList<ThemeFont> fonts, bool allowsSmallText)
    {
        Colors = colors;
        Typography = typography;
        Fonts = fonts;
        AllowsSmallText = allowsSmallText;
    }

    /// <summary>Gets the defaults of <c>site.css</c> as a theme, so a site without a theme of its own looks the same.</summary>
    public static SiteTheme Default { get; } = CreateDefault();

    /// <summary>Gets the colour tokens.</summary>
    public SiteColors Colors
    {
        get;
    }

    /// <summary>Gets the typography as given; <see cref="Effective" /> fills unset values from <see cref="Default" />.</summary>
    public SiteTypography Typography
    {
        get;
    }

    /// <summary>Gets the self-hosted fonts in key order.</summary>
    public IReadOnlyList<ThemeFont> Fonts
    {
        get;
    }

    /// <summary>Gets a value indicating whether the owner allowed body text below 1rem and other text below 0.875rem.</summary>
    public bool AllowsSmallText
    {
        get;
    }

    /// <summary>Creates a theme that meets every rule.</summary>
    /// <param name="colors">The colours.</param>
    /// <param name="typography">The typography.</param>
    /// <param name="fonts">The self-hosted fonts, if any.</param>
    /// <param name="allowSmallText">Whether body text below 1rem and other text below 0.875rem is allowed.</param>
    /// <returns>The theme, or validation errors by field path, such as <c>colors.muted</c> or <c>typography.body.lineHeight</c>.</returns>
    public static Result<SiteTheme> Create(SiteColors colors, SiteTypography typography, IEnumerable<ThemeFont>? fonts = null, bool allowSmallText = false)
    {
        var draft = CreateDraft(colors, typography, fonts, allowSmallText);
        if (!draft.TryGetValue(out var theme))
        {
            return draft;
        }

        var review = theme.Review();
        return review.HasErrors ? Result.Invalid<SiteTheme>("The site theme breaks a readability rule.", review.ToValidationErrors()) : draft;
    }

    /// <summary>Creates a theme for a preview, without the readability rules; <see cref="Review" /> reports them.</summary>
    /// <param name="colors">The colours.</param>
    /// <param name="typography">The typography.</param>
    /// <param name="fonts">The self-hosted fonts, if any.</param>
    /// <param name="allowSmallText">Whether body text below 1rem and other text below 0.875rem is allowed.</param>
    /// <returns>The theme, or errors on <c>fonts[i].key</c> for a repeated key and <c>typography.{role}.fontKey</c> for a missing font.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="colors" />, <paramref name="typography" /> or a font is <see langword="null" />.</exception>
    public static Result<SiteTheme> CreateDraft(SiteColors colors, SiteTypography typography, IEnumerable<ThemeFont>? fonts = null, bool allowSmallText = false)
    {
        ArgumentNullException.ThrowIfNull(colors);
        ArgumentNullException.ThrowIfNull(typography);
        var list = fonts?.ToArray() ?? [];
        if (list.Any(font => font is null))
        {
            throw new ArgumentNullException(nameof(fonts), "A font must not be null.");
        }

        var errors = new ThemeErrors();
        var keys = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < list.Length; index++)
        {
            if (!keys.Add(list[index].Key))
            {
                errors.Add($"fonts[{index}].key", "Another font already uses this key.");
            }
        }

        foreach (var role in Enum.GetValues<TypographyRole>())
        {
            if (typography[role].FontKey is { } key && !keys.Contains(key))
            {
                errors.Add($"typography.{ThemeNames.Css(role)}.fontKey", "No font has this key.");
            }
        }

        return errors.HasErrors
            ? errors.ToResult<SiteTheme>("The site theme is incomplete.")
            : Result.Ok(new SiteTheme(colors, typography, list.OrderBy(font => font.Key, StringComparer.Ordinal).ToArray(), allowSmallText));
    }

    /// <summary>Checks the readability rules.</summary>
    /// <returns>The errors, warnings and contrast checks.</returns>
    public SiteThemeReview Review() => SiteThemeRules.Review(this);

    /// <summary>Gets the style of a role with every unset value taken from <see cref="Default" />.</summary>
    /// <param name="role">The role.</param>
    /// <returns>The effective style.</returns>
    public TypographyStyle Effective(TypographyRole role) => Typography[role].Over(Default.Typography[role]);

    /// <summary>Gets the font a key references.</summary>
    /// <param name="key">The key.</param>
    /// <returns>The font, or <see langword="null" />.</returns>
    public ThemeFont? Font(string key) => Fonts.FirstOrDefault(font => string.Equals(font.Key, key, StringComparison.Ordinal));

    /// <inheritdoc />
    public override string ToString() => SiteThemeStylesheet.Hash(this);

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Colors;
        yield return Typography;
        yield return AllowsSmallText;
        yield return Fonts.Count;
        foreach (var font in Fonts)
        {
            yield return font;
        }
    }

    private static SiteTheme CreateDefault()
    {
        var colors = SiteColors.Create(
            primary: Pair("#1d4ed8", "#93c5fd"),
            onPrimary: Pair("#ffffff", "#111827"),
            accent: Pair("#b45309", "#fbbf24"),
            onAccent: Pair("#ffffff", "#111827"),
            text: Pair("#1f2937", "#f3f4f6"),
            muted: Pair("#4b5563", "#9ca3af"),
            surface: Pair("#ffffff", "#111827"),
            surfaceAlt: Pair("#f3f4f6", "#1f2937"),
            border: Pair("#d1d5db", "#374151"),
            focus: Pair("#1d4ed8", "#93c5fd"),
            primaryHover: Pair("#1e40af", "#60a5fa"),
            accentHover: Pair("#92400e", "#f59e0b"));
        var typography = SiteTypography.Create(new Dictionary<TypographyRole, TypographyStyle>
        {
            [TypographyRole.H1] = Heading(2.125m, 3.5m),
            [TypographyRole.H2] = Heading(1.75m, 2.5m),
            [TypographyRole.H3] = Heading(1.375m, 1.875m),
            [TypographyRole.H4] = Heading(1.125m, 1.375m),
            [TypographyRole.H5] = Heading(1m, 1.125m),
            [TypographyRole.H6] = Heading(0.875m, 0.9375m),
            [TypographyRole.Body] = TypographyStyle.Create(fallback: GenericFontFamily.SansSerif, size: Fluid(1m, 1.125m), weight: 400, lineHeight: 1.6m).Value,
            [TypographyRole.Small] = TypographyStyle.Create(size: Fluid(0.875m, 0.9375m)).Value,
            [TypographyRole.Button] = TypographyStyle.Create(weight: 600).Value,
        });
        return new SiteTheme(colors, typography, [], false);

        static ThemeColor Pair(string light, string dark) => ThemeColor.Create(CssColor.FromString(light), CssColor.FromString(dark));
        static FontSize Fluid(decimal min, decimal max) => FontSize.Fluid(min, max).Value;
        static TypographyStyle Heading(decimal min, decimal max) =>
            TypographyStyle.Create(fallback: GenericFontFamily.SansSerif, size: Fluid(min, max), weight: 700, lineHeight: 1.2m).Value;
    }
}
