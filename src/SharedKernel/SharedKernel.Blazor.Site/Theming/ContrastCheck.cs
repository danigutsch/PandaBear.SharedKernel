namespace SharedKernel.Blazor.Site.Theming;

/// <summary>The contrast of one colour pair that the components use, in one scheme, against its WCAG 2.2 minimum.</summary>
/// <param name="Foreground">The text or indicator colour.</param>
/// <param name="Background">The colour behind it.</param>
/// <param name="Scheme">The scheme.</param>
/// <param name="Ratio">The unrounded contrast ratio.</param>
/// <param name="Minimum">4.5 for text (SC 1.4.3), 3 for the focus indicator (SC 1.4.11).</param>
/// <param name="Criterion">The success criterion number, <c>1.4.3</c> or <c>1.4.11</c>.</param>
public sealed record ContrastCheck(
    ThemeColorRole Foreground,
    ThemeColorRole Background,
    ColorScheme Scheme,
    double Ratio,
    double Minimum,
    string Criterion)
{
    /// <summary>Gets a value indicating whether the ratio reaches the minimum; the ratio is not rounded first.</summary>
    public bool Passes => Ratio >= Minimum;
}
