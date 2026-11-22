namespace PandaBear.Blazor.Site.Theming;

/// <summary>
/// The darkening of a photo behind on-primary text: the least opacity, in whole percent, at which the theme's text colour laid over
/// any photo keeps the on-primary colour readable, computed from the light colours because photo bands always use the light scheme.
/// Any photo means the worst one: white for a light on-primary colour, black for a dark one, so the values hold for whatever image
/// the owner uploads. <c>site.css</c> and <see cref="SiteThemeStylesheet" /> write the values as <c>--site-scrim</c>,
/// <c>--site-scrim-large</c> and <c>--site-scrim-boost</c> for <c>color-mix(in srgb, var(--site-color-text) var(--site-scrim),
/// transparent)</c>.
/// </summary>
public sealed class SiteScrim
{
    /// <summary>The contrast that normal text needs (WCAG 2.2 SC 1.4.3).</summary>
    public const double TextMinimum = 4.5;

    /// <summary>The contrast that large text needs: 18pt, or 14pt bold (WCAG 2.2 SC 1.4.3).</summary>
    public const double LargeTextMinimum = 3.0;

    private SiteScrim(int text, int largeText, int boost, bool isReachable)
    {
        Text = text;
        LargeText = largeText;
        Boost = boost;
        IsReachable = isReachable;
    }

    /// <summary>Gets the opacity in percent at which normal text reaches 4.5:1; 100 when no opacity reaches it.</summary>
    public int Text { get; }

    /// <summary>Gets the opacity in percent at which large text reaches 3:1; 100 when no opacity reaches it.</summary>
    public int LargeText { get; }

    /// <summary>
    /// Gets the opacity in percent of a second layer that, over a photo already darkened by <see cref="LargeText" />, reaches at
    /// least <see cref="Text" />: normal text, such as the navigation of a header over a hero, on a large-text scrim.
    /// </summary>
    public int Boost { get; }

    /// <summary>Gets a value indicating whether an opacity reaches 4.5:1 at all; when not, the theme review reports an error.</summary>
    public bool IsReachable { get; }

    /// <summary>Computes the scrim of a theme's colours: its light text colour darkening a photo behind its light on-primary colour.</summary>
    /// <param name="colors">The colours.</param>
    /// <returns>The scrim; an unreachable value is 100, the best a draft can show.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="colors" /> is <see langword="null" />.</exception>
    public static SiteScrim For(SiteColors colors)
    {
        ArgumentNullException.ThrowIfNull(colors);
        var foreground = colors.OnPrimary.Light;
        var scrim = colors.Text.Light;
        var text = MinimumPercent(foreground, scrim, TextMinimum);
        var large = MinimumPercent(foreground, scrim, LargeTextMinimum) ?? 100;
        var strong = text ?? 100;
        var boost = large >= 100 ? 0 : (int)Math.Ceiling(Math.Round(100.0 - ((100.0 - strong) * 100.0 / (100.0 - large)), 9));
        return new SiteScrim(strong, large, boost, text is not null);
    }

    /// <summary>Finds the least whole percent of a scrim colour at which a text colour reaches a contrast over any photo.</summary>
    /// <param name="foreground">The text colour.</param>
    /// <param name="scrim">The darkening colour.</param>
    /// <param name="minimum">The contrast to reach, such as 4.5 or 3.</param>
    /// <returns>The percent from 0 to 100, or <see langword="null" /> when even an opaque scrim does not reach it.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="foreground" /> or <paramref name="scrim" /> is <see langword="null" />.</exception>
    public static int? MinimumPercent(CssColor foreground, CssColor scrim, double minimum)
    {
        ArgumentNullException.ThrowIfNull(foreground);
        ArgumentNullException.ThrowIfNull(scrim);
        for (var percent = 0; percent <= 100; percent++)
        {
            if (WorstRatio(foreground, scrim, percent) >= minimum)
            {
                return percent;
            }
        }

        return null;
    }

    /// <summary>
    /// Computes the lowest contrast of a text colour over any photo darkened by a scrim colour at an opacity. The darkened photo lies
    /// between the scrim over black and the scrim over white; text whose luminance lies between those two can meet a photo of its
    /// own luminance, which gives 1.
    /// </summary>
    /// <param name="foreground">The text colour.</param>
    /// <param name="scrim">The darkening colour.</param>
    /// <param name="percent">The opacity, from 0 to 100.</param>
    /// <returns>The ratio, from 1 to 21.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="foreground" /> or <paramref name="scrim" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="percent" /> is not from 0 to 100.</exception>
    public static double WorstRatio(CssColor foreground, CssColor scrim, int percent)
    {
        ArgumentNullException.ThrowIfNull(foreground);
        ArgumentNullException.ThrowIfNull(scrim);
        ArgumentOutOfRangeException.ThrowIfNegative(percent);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(percent, 100);
        var overWhite = scrim.Over(percent, overWhite: true);
        var overBlack = scrim.Over(percent, overWhite: false);
        if (foreground.RelativeLuminance >= overWhite.RelativeLuminance)
        {
            return foreground.ContrastRatio(overWhite);
        }

        return foreground.RelativeLuminance <= overBlack.RelativeLuminance ? foreground.ContrastRatio(overBlack) : 1.0;
    }
}
