using System.Globalization;

namespace PandaBear.Blazor.Site.Theming;

/// <summary>
/// The readability rules of a theme. Contrast follows WCAG 2.2: 4.5:1 for text (SC 1.4.3 Contrast (Minimum)) and 3:1 for the focus
/// indicator (SC 1.4.11 Non-text Contrast), in both schemes.
/// </summary>
internal static class SiteThemeRules
{
    /// <summary>The smallest body size in rem unless small text is allowed.</summary>
    public const decimal MinBodySize = 1m;

    /// <summary>The smallest size in rem of every other role unless small text is allowed.</summary>
    public const decimal MinOtherSize = 0.875m;

    /// <summary>The smallest body line height.</summary>
    public const decimal MinBodyLineHeight = 1.3m;

    /// <summary>The largest body line height.</summary>
    public const decimal MaxBodyLineHeight = 2m;

    /// <summary>The largest ratio of a fluid size's maximum to its minimum, so 200% zoom still doubles the text (SC 1.4.4).</summary>
    public const decimal MaxFluidRatio = 2.5m;

    private const double TextMinimum = 4.5;
    private const double NonTextMinimum = 3.0;

    /// <summary>Gets the colour pairs the components draw, with their minimum and criterion.</summary>
    public static IReadOnlyList<(ThemeColorRole Foreground, ThemeColorRole Background, double Minimum, string Criterion)> Pairs { get; } =
    [
        (ThemeColorRole.Text, ThemeColorRole.Surface, TextMinimum, "1.4.3"),
        (ThemeColorRole.Text, ThemeColorRole.SurfaceAlt, TextMinimum, "1.4.3"),
        (ThemeColorRole.Muted, ThemeColorRole.Surface, TextMinimum, "1.4.3"),
        (ThemeColorRole.Muted, ThemeColorRole.SurfaceAlt, TextMinimum, "1.4.3"),
        (ThemeColorRole.Primary, ThemeColorRole.Surface, TextMinimum, "1.4.3"),
        (ThemeColorRole.Primary, ThemeColorRole.SurfaceAlt, TextMinimum, "1.4.3"),
        (ThemeColorRole.PrimaryHover, ThemeColorRole.Surface, TextMinimum, "1.4.3"),
        (ThemeColorRole.PrimaryHover, ThemeColorRole.SurfaceAlt, TextMinimum, "1.4.3"),
        (ThemeColorRole.OnPrimary, ThemeColorRole.Primary, TextMinimum, "1.4.3"),
        (ThemeColorRole.OnPrimary, ThemeColorRole.PrimaryHover, TextMinimum, "1.4.3"),
        (ThemeColorRole.OnAccent, ThemeColorRole.Accent, TextMinimum, "1.4.3"),
        (ThemeColorRole.OnAccent, ThemeColorRole.AccentHover, TextMinimum, "1.4.3"),
        (ThemeColorRole.Focus, ThemeColorRole.Surface, NonTextMinimum, "1.4.11"),
        (ThemeColorRole.Focus, ThemeColorRole.SurfaceAlt, NonTextMinimum, "1.4.11"),
    ];

    /// <summary>Checks a theme.</summary>
    /// <param name="theme">The theme.</param>
    /// <param name="colorLabel">The host's names of the colour roles in contrast messages; a role without one keeps its CSS name.</param>
    /// <returns>The review.</returns>
    public static SiteThemeReview Review(SiteTheme theme, Func<ThemeColorRole, string?>? colorLabel = null)
    {
        var errors = new List<ThemeIssue>();
        var warnings = new List<ThemeIssue>();
        var contrast = CheckContrast(theme.Colors);
        foreach (var check in contrast.Where(check => !check.Passes))
        {
            var kind = check.Criterion == "1.4.11" ? "a focus indicator" : "text";
            errors.Add(new ThemeIssue(
                "colors." + ThemeNames.Field(check.Foreground),
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{ThemeNames.Label(check.Foreground, colorLabel)} on {ThemeNames.Label(check.Background, colorLabel)} is {FormatRatio(check.Ratio)} in the {ThemeNames.Css(check.Scheme)} scheme; {kind} needs at least {FormatMinimum(check.Minimum)} (WCAG 2.2 SC {check.Criterion}).")));
        }

        CheckSizes(theme, errors);
        CheckBodyLineHeight(theme, errors);
        CheckHeadingOrder(theme, warnings);
        return new SiteThemeReview(errors, warnings, contrast);
    }

    /// <summary>Formats a ratio rounded down to two decimals, so a failing 4.499 never reads as 4.50.</summary>
    /// <param name="ratio">The ratio.</param>
    /// <returns>Text such as <c>4.49:1</c>.</returns>
    public static string FormatRatio(double ratio) =>
        (Math.Floor(ratio * 100) / 100).ToString("0.00", CultureInfo.InvariantCulture) + ":1";

    /// <summary>Formats a minimum ratio.</summary>
    /// <param name="minimum">The minimum.</param>
    /// <returns>Text such as <c>4.5:1</c> or <c>3:1</c>.</returns>
    public static string FormatMinimum(double minimum) => minimum.ToString("0.#", CultureInfo.InvariantCulture) + ":1";

    private static ContrastCheck[] CheckContrast(SiteColors colors) =>
        [.. from scheme in Enum.GetValues<ColorScheme>()
            from pair in Pairs
            select new ContrastCheck(
                pair.Foreground,
                pair.Background,
                scheme,
                colors[pair.Foreground].For(scheme).ContrastRatio(colors[pair.Background].For(scheme)),
                pair.Minimum,
                pair.Criterion)];

    private static void CheckSizes(SiteTheme theme, List<ThemeIssue> errors)
    {
        foreach (var role in Enum.GetValues<TypographyRole>())
        {
            if (theme.Effective(role).Size is not { } size)
            {
                continue;
            }

            var field = "typography." + ThemeNames.Css(role) + ".size";
            var floor = role == TypographyRole.Body ? MinBodySize : MinOtherSize;
            if (!theme.AllowsSmallText && size.Min < floor)
            {
                var what = role == TypographyRole.Body ? "Body text" : "Text";
                errors.Add(new ThemeIssue(field, $"{what} must be at least {CssText.Number(floor)}rem unless small text is allowed."));
            }

            if (size.IsFluid && size.Max > size.Min * MaxFluidRatio)
            {
                errors.Add(new ThemeIssue(field, $"The largest size of a fluid size must be at most {CssText.Number(MaxFluidRatio)} times its smallest, so zoom can double the text (WCAG 2.2 SC 1.4.4)."));
            }
        }
    }

    private static void CheckBodyLineHeight(SiteTheme theme, List<ThemeIssue> errors)
    {
        if (theme.Effective(TypographyRole.Body).LineHeight is { } lineHeight && (lineHeight < MinBodyLineHeight || lineHeight > MaxBodyLineHeight))
        {
            errors.Add(new ThemeIssue(
                "typography.body.lineHeight",
                $"The body line height must be from {CssText.Number(MinBodyLineHeight)} to {CssText.Number(MaxBodyLineHeight)}."));
        }
    }

    private static void CheckHeadingOrder(SiteTheme theme, List<ThemeIssue> warnings)
    {
        for (var level = TypographyRole.H2; level <= TypographyRole.H6; level++)
        {
            var above = theme.Effective(level - 1).Size;
            var current = theme.Effective(level).Size;
            if (above is not null && current is not null && (current.Min > above.Min || current.Max > above.Max))
            {
                warnings.Add(new ThemeIssue(
                    "typography." + ThemeNames.Css(level) + ".size",
                    $"{ThemeNames.Css(level)} is larger than {ThemeNames.Css(level - 1)}; headings usually get smaller with each level."));
            }
        }
    }
}
