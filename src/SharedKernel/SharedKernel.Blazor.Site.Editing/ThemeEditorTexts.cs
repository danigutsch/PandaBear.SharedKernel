using SharedKernel.Blazor.Site.Theming;

namespace SharedKernel.Blazor.Site.Editing;

/// <summary>
/// The texts of <see cref="ThemeEditor" />: legends, labels, help and the names of the roles and values, in English by default, so the
/// host can reword or localize them. Pass the same <see cref="ColorName" /> to <see cref="ThemeEditorModel.ToTheme" /> and
/// <see cref="ThemePreview.ColorLabel" />, so contrast messages name the colors as the editor does.
/// </summary>
public sealed class ThemeEditorTexts
{
    /// <summary>Gets the legend of the colors.</summary>
    public string ColorsLegend { get; init; } = "Colors";

    /// <summary>Gets the help of the colors.</summary>
    public string ColorsHelp { get; init; } =
        "Each color has a value for the light and for the dark scheme, written as #rrggbb. Leave both values of a hover color empty to derive it from its base color.";

    /// <summary>Gets the label of a light-scheme value.</summary>
    public string Light { get; init; } = "Light";

    /// <summary>Gets the label of a dark-scheme value.</summary>
    public string Dark { get; init; } = "Dark";

    /// <summary>Gets the legend of the typography.</summary>
    public string TypographyLegend { get; init; } = "Typography";

    /// <summary>Gets the help of the typography.</summary>
    public string TypographyHelp { get; init; } =
        "An empty value keeps the default. Sizes are in rem (1rem is the reader's default text size); with a largest size, the text grows from the smallest on a phone to the largest on a wide screen.";

    /// <summary>Gets the label of a role's font.</summary>
    public string Font { get; init; } = "Font";

    /// <summary>Gets the option of no self-hosted font.</summary>
    public string SystemFont { get; init; } = "Default (system font)";

    /// <summary>Gets the label of a role's system font stack, written after its font.</summary>
    public string Fallback { get; init; } = "System font after it";

    /// <summary>Gets the option of a select that keeps the default.</summary>
    public string Default { get; init; } = "Default";

    /// <summary>Gets the label of a role's size.</summary>
    public string Size { get; init; } = "Size (rem)";

    /// <summary>Gets the label of a role's largest size.</summary>
    public string SizeMax { get; init; } = "Largest size (rem, optional)";

    /// <summary>Gets the label of a role's weight.</summary>
    public string Weight { get; init; } = "Weight";

    /// <summary>Gets the label of a role's line height.</summary>
    public string LineHeight { get; init; } = "Line height";

    /// <summary>Gets the label of a role's letter spacing.</summary>
    public string LetterSpacing { get; init; } = "Letter spacing (em)";

    /// <summary>Gets the label of a role's text transform.</summary>
    public string Transform { get; init; } = "Letter case";

    /// <summary>Gets the label of a role's style.</summary>
    public string Style { get; init; } = "Style";

    /// <summary>Gets the option of upright text.</summary>
    public string Normal { get; init; } = "Normal";

    /// <summary>Gets the option of italic text.</summary>
    public string Italic { get; init; } = "Italic";

    /// <summary>Gets the label of the color of a heading, the card title or the brand.</summary>
    public string HeadingColor { get; init; } = "Color";

    /// <summary>Gets the label of the switch that allows small text.</summary>
    public string AllowSmallText { get; init; } = "Allow body text below 1rem and other text below 0.875rem";

    /// <summary>Gets the legend of the header.</summary>
    public string HeaderLegend { get; init; } = "Header";

    /// <summary>Gets the label of the logo height.</summary>
    public string BrandHeight { get; init; } = "Logo height (rem)";

    /// <summary>Gets the help of the logo height.</summary>
    public string BrandHeightHelp { get; init; } =
        "The height of the logo in the header, from 1.5 to 8; the header grows with it. Empty keeps the default.";

    /// <summary>Gets the legend of the shape.</summary>
    public string ShapeLegend { get; init; } = "Shape";

    /// <summary>Gets the help of the shape.</summary>
    public string ShapeHelp { get; init; } =
        "The corners of buttons, cards and images, and the divider under section headings and in the hero. An empty value keeps the default.";

    /// <summary>Gets the label of the corner shape.</summary>
    public string ControlShape { get; init; } = "Corners";

    /// <summary>Gets the label of the divider width.</summary>
    public string DividerWidth { get; init; } = "Divider width (rem)";

    /// <summary>Gets the label of the divider thickness.</summary>
    public string DividerThickness { get; init; } = "Divider thickness (rem)";

    /// <summary>Gets the label of the divider color.</summary>
    public string DividerColor { get; init; } = "Divider color";

    /// <summary>
    /// Gets the message of a number field whose text is not a number, from the field's name (its label without a final part in
    /// brackets, such as <c>Line height</c>) and a valid example, such as <c>1.5</c>.
    /// </summary>
    public Func<string, string, string> NotANumber { get; init; } = (name, example) => $"{name} must be a number, like {example}.";

    /// <summary>
    /// Gets the name of a color role: the start of its legend, the name of its option in the heading and divider color lists, and, passed
    /// on as a color label, its name in contrast messages, such as <c>Card background</c> for <see cref="ThemeColorRole.SurfaceAlt" />.
    /// </summary>
    public Func<ThemeColorRole, string> ColorName { get; init; } = DefaultColorName;

    /// <summary>Gets what a color role colors, or that it is optional, written after its name in its legend; <see langword="null" /> for none.</summary>
    public Func<ThemeColorRole, string?> ColorHint { get; init; } = DefaultColorHint;

    /// <summary>Gets the name of a typography role, the legend of its fields.</summary>
    public Func<TypographyRole, string> TypographyName { get; init; } = DefaultTypographyName;

    /// <summary>Gets the name of a system font stack.</summary>
    public Func<GenericFontFamily, string> FallbackName { get; init; } = DefaultFallbackName;

    /// <summary>Gets the name of a text transform.</summary>
    public Func<ThemeTextTransform, string> TransformName { get; init; } = DefaultTransformName;

    /// <summary>Gets the name of a corner shape.</summary>
    public Func<Theming.ControlShape, string> ControlShapeName { get; init; } = DefaultControlShapeName;

    /// <summary>Gets the legend of a color role: its <see cref="ColorName" />, followed by its <see cref="ColorHint" /> in brackets.</summary>
    /// <param name="role">The role.</param>
    /// <returns>The legend, such as <c>Primary (links, main buttons, current menu item)</c>.</returns>
    public string ColorLegend(ThemeColorRole role) => ColorHint(role) is { Length: > 0 } hint ? $"{ColorName(role)} ({hint})" : ColorName(role);

    private static string DefaultColorName(ThemeColorRole role) => role switch
    {
        ThemeColorRole.Primary => "Primary",
        ThemeColorRole.PrimaryHover => "Primary hover",
        ThemeColorRole.OnPrimary => "Text on primary",
        ThemeColorRole.Accent => "Accent",
        ThemeColorRole.AccentHover => "Accent hover",
        ThemeColorRole.OnAccent => "Text on accent",
        ThemeColorRole.Text => "Text",
        ThemeColorRole.Muted => "Muted text",
        ThemeColorRole.Surface => "Page background",
        ThemeColorRole.SurfaceAlt => "Card background",
        ThemeColorRole.Border => "Borders",
        ThemeColorRole.Focus => "Keyboard focus ring",
        _ => role.ToString(),
    };

    private static string? DefaultColorHint(ThemeColorRole role) => role switch
    {
        ThemeColorRole.Primary => "links, main buttons, current menu item",
        ThemeColorRole.Accent => "dividers, second buttons",
        ThemeColorRole.PrimaryHover or ThemeColorRole.AccentHover => "optional",
        _ => null,
    };

    private static string DefaultTypographyName(TypographyRole role) => role switch
    {
        TypographyRole.H1 => "Heading 1",
        TypographyRole.H2 => "Heading 2",
        TypographyRole.H3 => "Heading 3",
        TypographyRole.H4 => "Heading 4",
        TypographyRole.H5 => "Heading 5",
        TypographyRole.H6 => "Heading 6",
        TypographyRole.Body => "Body text",
        TypographyRole.Small => "Small print",
        TypographyRole.Link => "Links",
        TypographyRole.Button => "Buttons",
        TypographyRole.Nav => "Menu",
        TypographyRole.CardTitle => "Card titles",
        TypographyRole.Brand => "Site name (without a logo)",
        _ => role.ToString(),
    };

    private static string DefaultFallbackName(GenericFontFamily family) => family switch
    {
        GenericFontFamily.Serif => "Serif",
        GenericFontFamily.Monospace => "Monospace",
        _ => "Sans-serif",
    };

    private static string DefaultTransformName(ThemeTextTransform transform) => transform switch
    {
        ThemeTextTransform.Uppercase => "UPPER CASE",
        ThemeTextTransform.Capitalize => "Capitalized Words",
        _ => "As written",
    };

    private static string DefaultControlShapeName(Theming.ControlShape shape) => shape switch
    {
        Theming.ControlShape.Square => "Square",
        Theming.ControlShape.Pill => "Pill (round ends)",
        _ => "Rounded",
    };
}
