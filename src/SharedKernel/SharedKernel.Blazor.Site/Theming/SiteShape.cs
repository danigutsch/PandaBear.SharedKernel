using SharedKernel.Domain;
using SharedKernel.Results;

namespace SharedKernel.Blazor.Site.Theming;

/// <summary>
/// The shape of a theme: the <see cref="ControlShape" /> of controls and surfaces, and the decorative divider that a section draws
/// under its heading and a divided hero draws beside its title, so both rules have one width, thickness and colour.
/// </summary>
public sealed class SiteShape : ValueObject
{
    /// <summary>The divider width in rem of <see cref="Default" />.</summary>
    public const decimal DefaultDividerWidth = 4m;

    /// <summary>The divider thickness in rem of <see cref="Default" />.</summary>
    public const decimal DefaultDividerThickness = 0.25m;

    private SiteShape(ControlShape control, decimal dividerWidth, decimal dividerThickness, ThemeColorRole dividerColor)
    {
        Control = control;
        DividerWidth = dividerWidth;
        DividerThickness = dividerThickness;
        DividerColor = dividerColor;
    }

    /// <summary>Gets the colours the divider may have: the primary, accent, text, muted and border colour.</summary>
    public static IReadOnlyList<ThemeColorRole> DividerColors { get; } =
        [ThemeColorRole.Primary, ThemeColorRole.Accent, ThemeColorRole.Text, ThemeColorRole.Muted, ThemeColorRole.Border];

    /// <summary>Gets the shape of <c>site.css</c> without a theme: rounded, with a 4rem accent divider 0.25rem thick.</summary>
    public static SiteShape Default { get; } = new(ControlShape.Rounded, DefaultDividerWidth, DefaultDividerThickness, ThemeColorRole.Accent);

    /// <summary>Gets the corner shape of controls and surfaces.</summary>
    public ControlShape Control { get; }

    /// <summary>Gets the divider width in rem.</summary>
    public decimal DividerWidth { get; }

    /// <summary>Gets the divider thickness in rem.</summary>
    public decimal DividerThickness { get; }

    /// <summary>Gets the colour role of the divider on the page; on the primary colour or a photo the divider takes the text's colour.</summary>
    public ThemeColorRole DividerColor { get; }

    /// <summary>Creates a shape; the theme's rules (see <see cref="SiteTheme.Review" />) check the divider's layout range.</summary>
    /// <param name="control">The corner shape.</param>
    /// <param name="dividerWidth">The divider width in rem: positive, with at most four decimals.</param>
    /// <param name="dividerThickness">The divider thickness in rem: positive, with at most four decimals.</param>
    /// <param name="dividerColor">The divider colour: the primary, accent, text, muted or border colour.</param>
    /// <returns>The shape, or validation errors on the fields <c>control</c>, <c>dividerWidth</c>, <c>dividerThickness</c> and <c>dividerColor</c>.</returns>
    public static Result<SiteShape> Create(
        ControlShape control = ControlShape.Rounded,
        decimal dividerWidth = DefaultDividerWidth,
        decimal dividerThickness = DefaultDividerThickness,
        ThemeColorRole dividerColor = ThemeColorRole.Accent)
    {
        var errors = new ThemeErrors();
        if (!Enum.IsDefined(control))
        {
            errors.Add("control", "The control shape must be square, rounded or pill.");
        }

        if (!IsRem(dividerWidth))
        {
            errors.Add("dividerWidth", "The divider width must be a positive size in rem with at most four decimals.");
        }

        if (!IsRem(dividerThickness))
        {
            errors.Add("dividerThickness", "The divider thickness must be a positive size in rem with at most four decimals.");
        }

        if (!DividerColors.Contains(dividerColor))
        {
            errors.Add("dividerColor", "The divider colour must be the primary, accent, text, muted or border colour.");
        }

        return errors.HasErrors
            ? errors.ToResult<SiteShape>("The site shape is invalid.")
            : Result.Ok(new SiteShape(control, dividerWidth, dividerThickness, dividerColor));
    }

    /// <inheritdoc />
    public override string ToString() => $"{Control} {CssText.Number(DividerWidth)}rem {CssText.Number(DividerThickness)}rem {DividerColor}";

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Control;
        yield return DividerWidth;
        yield return DividerThickness;
        yield return DividerColor;
    }

    private static bool IsRem(decimal value) => value > 0 && CssText.Round(value) == value;
}
