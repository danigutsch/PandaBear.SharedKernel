using SharedKernel.BuildingBlocks;
using SharedKernel.Results;

namespace SharedKernel.Blazor.Site.Theming;

/// <summary>
/// An owner-editable site theme: colour tokens with a light and a dark value, typography per role, self-hosted fonts, the
/// height of the brand image in the header, from which the header's own height follows, and the <see cref="SiteShape" /> of
/// controls, surfaces and dividers.
/// <see cref="Create" /> enforces every rule (see <see cref="Review" />); <see cref="CreateDraft" /> only enforces what rendering
/// needs (unique font keys and existing font references), so an editor can preview a theme that still breaks a rule.
/// </summary>
public sealed class SiteTheme : ValueObject
{
    /// <summary>The brand height in rem of a theme that sets none: 2.5rem (40px).</summary>
    public const decimal DefaultBrandHeight = 2.5m;

    private const decimal HeaderPadding = 2m;
    private const decimal NavigationLinkPadding = 0.5m;
    private const decimal FallbackNavigationSize = 1.125m;
    private const decimal FallbackLineHeight = 1.6m;

    private SiteTheme(SiteColors colors, SiteTypography typography, IReadOnlyList<ThemeFont> fonts, bool allowsSmallText, decimal brandHeight, SiteShape shape)
    {
        BrandHeight = brandHeight;
        Shape = shape;
        Colors = colors;
        Typography = typography;
        Fonts = fonts;
        AllowsSmallText = allowsSmallText;
    }

    /// <summary>Gets the defaults of <c>site.css</c> as a theme, so a site without a theme of its own looks the same.</summary>
    public static SiteTheme Default { get; } = CreateDefault();

    /// <summary>Gets the colour tokens.</summary>
    public SiteColors Colors { get; }

    /// <summary>Gets the typography as given; <see cref="Effective" /> fills unset values from <see cref="Default" />.</summary>
    public SiteTypography Typography { get; }

    /// <summary>Gets the self-hosted fonts in key order.</summary>
    public IReadOnlyList<ThemeFont> Fonts { get; }

    /// <summary>Gets the shape of controls, surfaces and dividers.</summary>
    public SiteShape Shape { get; }

    /// <summary>Gets the height in rem of the brand image (the logo) in the header.</summary>
    public decimal BrandHeight { get; }

    /// <summary>
    /// Gets the height in rem of the header in one row, which a cover hero leaves free for a header over it: the header's block
    /// padding (2 × 1rem) plus the taller of <see cref="BrandHeight" /> and a navigation line. That line is the largest navigation
    /// font size (the body's when the navigation role sets none) times its line height, plus the links' block padding (2 × 0.25rem),
    /// rounded up to four decimals.
    /// </summary>
    public decimal HeaderHeight
    {
        get
        {
            var navigation = Effective(TypographyRole.Nav);
            var body = Effective(TypographyRole.Body);
            var size = (navigation.Size ?? body.Size)?.Max ?? FallbackNavigationSize;
            var lineHeight = navigation.LineHeight ?? body.LineHeight ?? FallbackLineHeight;
            var height = HeaderPadding + Math.Max(BrandHeight, (size * lineHeight) + NavigationLinkPadding);
            return Math.Ceiling(height * 10000m) / 10000m;
        }
    }

    /// <summary>Gets a value indicating whether the owner allowed body text below 1rem and other text below 0.875rem.</summary>
    public bool AllowsSmallText { get; }

    /// <summary>Creates a theme that meets every rule.</summary>
    /// <param name="colors">The colours.</param>
    /// <param name="typography">The typography.</param>
    /// <param name="fonts">The self-hosted fonts, if any.</param>
    /// <param name="allowSmallText">Whether body text below 1rem and other text below 0.875rem is allowed.</param>
    /// <param name="brandHeight">The height in rem of the brand image in the header, from 1.5rem to 8rem.</param>
    /// <param name="colorLabel">The names of the colour roles in contrast messages, as in <see cref="Review" />.</param>
    /// <param name="shape">The shape of controls, surfaces and dividers; <see cref="SiteShape.Default" /> when <see langword="null" />.</param>
    /// <returns>The theme, or validation errors by field path, such as <c>colors.muted</c> or <c>typography.body.lineHeight</c>.</returns>
    public static Result<SiteTheme> Create(
        SiteColors colors,
        SiteTypography typography,
        IEnumerable<ThemeFont>? fonts = null,
        bool allowSmallText = false,
        decimal brandHeight = DefaultBrandHeight,
        Func<ThemeColorRole, string?>? colorLabel = null,
        SiteShape? shape = null)
    {
        var draft = CreateDraft(colors, typography, fonts, allowSmallText, brandHeight, shape);
        if (!draft.TryGetValue(out var theme))
        {
            return draft;
        }

        var review = theme.Review(colorLabel);
        return review.HasErrors ? Result.Invalid<SiteTheme>("The site theme breaks a readability rule.", review.ToValidationErrors()) : draft;
    }

    /// <summary>Creates a theme for a preview, without the readability rules; <see cref="Review" /> reports them.</summary>
    /// <param name="colors">The colours.</param>
    /// <param name="typography">The typography.</param>
    /// <param name="fonts">The self-hosted fonts, if any.</param>
    /// <param name="allowSmallText">Whether body text below 1rem and other text below 0.875rem is allowed.</param>
    /// <param name="brandHeight">The height in rem of the brand image in the header; a draft only needs it positive.</param>
    /// <param name="shape">The shape of controls, surfaces and dividers; <see cref="SiteShape.Default" /> when <see langword="null" />.</param>
    /// <returns>
    /// The theme, or errors on <c>fonts[i].key</c> for a repeated key, <c>typography.{role}.fontKey</c> for a missing font,
    /// <c>typography.{role}.color</c> for a colour on a role that is not a heading, and <c>header.brandHeight</c> for a brand height
    /// that is not positive or has more than four decimals.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="colors" />, <paramref name="typography" /> or a font is <see langword="null" />.</exception>
    public static Result<SiteTheme> CreateDraft(
        SiteColors colors,
        SiteTypography typography,
        IEnumerable<ThemeFont>? fonts = null,
        bool allowSmallText = false,
        decimal brandHeight = DefaultBrandHeight,
        SiteShape? shape = null)
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
                errors.Add($"typography.{ThemeNames.Field(role)}.fontKey", "No font has this key.");
            }

            if (typography[role].Color is not null && !ThemeNames.TakesColor(role))
            {
                errors.Add($"typography.{ThemeNames.Field(role)}.color", "Only headings, card titles and the brand take a colour; the components colour this text.");
            }
        }

        if (brandHeight <= 0 || CssText.Round(brandHeight) != brandHeight)
        {
            errors.Add("header.brandHeight", "The brand height must be a positive size in rem with at most four decimals.");
        }

        return errors.HasErrors
            ? errors.ToResult<SiteTheme>("The site theme is incomplete.")
            : Result.Ok(new SiteTheme(colors, typography, list.OrderBy(font => font.Key, StringComparer.Ordinal).ToArray(), allowSmallText, brandHeight, shape ?? SiteShape.Default));
    }

    /// <summary>Checks the readability rules.</summary>
    /// <param name="colorLabel">
    /// The names a reader sees for the colour roles in contrast messages, such as <c>Card background</c> for
    /// <see cref="ThemeColorRole.SurfaceAlt" />, so the messages read like the host's editor. A role it gives <see langword="null" />
    /// or white space for, or every role when it is <see langword="null" />, keeps its CSS name, such as <c>surface-alt</c>. Field
    /// paths do not change.
    /// </param>
    /// <returns>The errors, warnings and contrast checks.</returns>
    public SiteThemeReview Review(Func<ThemeColorRole, string?>? colorLabel = null) => SiteThemeRules.Review(this, colorLabel);

    /// <summary>Gets the style of a role with every unset value taken from <see cref="Default" />.</summary>
    /// <param name="role">The role.</param>
    /// <returns>The effective style.</returns>
    /// <remarks>
    /// A card title's unset font family, weight and style come from this theme's own level 3 heading before the default, so a theme
    /// that gives its headings a display font keeps it on its card titles; its size, line height, spacing and transform stay the card
    /// title's. A text brand's come from the level 1 heading the same way.
    /// </remarks>
    public TypographyStyle Effective(TypographyRole role) => role switch
    {
        TypographyRole.CardTitle => Typography[role].Over(Typography[TypographyRole.H3].Face()).Over(Default.Typography[role]),
        TypographyRole.Brand => Typography[role].Over(Typography[TypographyRole.H1].Face()).Over(Default.Typography[role]),
        _ => Typography[role].Over(Default.Typography[role]),
    };

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
        yield return BrandHeight;
        yield return Shape;
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
            [TypographyRole.CardTitle] = Heading(1.125m, 1.375m),
            [TypographyRole.Brand] = TypographyStyle.Create(size: Fluid(1.125m, 1.375m), lineHeight: 1.2m).Value,
        });
        return new SiteTheme(colors, typography, [], false, DefaultBrandHeight, SiteShape.Default);

        static ThemeColor Pair(string light, string dark) => ThemeColor.Create(CssColor.FromString(light), CssColor.FromString(dark));
        static FontSize Fluid(decimal min, decimal max) => FontSize.Fluid(min, max).Value;
        static TypographyStyle Heading(decimal min, decimal max) =>
            TypographyStyle.Create(fallback: GenericFontFamily.SansSerif, size: Fluid(min, max), weight: 700, lineHeight: 1.2m).Value;
    }
}
