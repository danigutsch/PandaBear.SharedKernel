using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using SharedKernel.Blazor.Site.Theming;

namespace SharedKernel.Blazor.Site.Editing;

/// <summary>
/// The fields of an owner-editable site theme, bound to a <see cref="ThemeEditorModel" />: every colour role with its light and dark
/// value, every typography role (font, fallback, sizes, weight, line height, letter spacing, case, style and, for headings, the card
/// title and the brand, the colour), the small-text switch, the logo height and the shape. Render it inside the host's
/// <c>EditForm</c>, between an <see cref="ErrorSummary" /> and <see cref="FormActions" />: each control's id is
/// <see cref="FieldIds.For" />(<see cref="IdPrefix" />, field path), its messages are the edit context's for that path, and
/// <see cref="Fields" /> lists every path, so a <see cref="ServerFormSubmission" /> places the theme's rule errors on their controls
/// and its summary links to them. A number that is not a number is an error on its field when the form validates.
/// </summary>
/// <remarks>
/// The editor needs an interactive render mode. It writes no style attributes and no script: a colour's swatch is an SVG fill. The host
/// owns the form, the save, the preview and the font library; it passes the families to offer in <see cref="Fonts" /> and fills
/// <see cref="ThemeEditorModel.Fonts" /> from its library before building the theme.
/// </remarks>
public sealed partial class ThemeEditor : IDisposable
{
    private const string LightSuffix = ".light";
    private const string DarkSuffix = ".dark";
    private const string FontKeySuffix = ".fontKey";
    private const string FallbackSuffix = ".fallback";
    private const string SizeSuffix = ".size";
    private const string SizeMaxSuffix = ".sizeMax";
    private const string WeightSuffix = ".weight";
    private const string LineHeightSuffix = ".lineHeight";
    private const string LetterSpacingSuffix = ".letterSpacing";
    private const string TransformSuffix = ".transform";
    private const string ItalicSuffix = ".italic";
    private const string ColorSuffix = ".color";
    private const string NormalValue = "normal";
    private const string ItalicOptionValue = "italic";

    private static readonly ThemeColorRole[] ColorRoles = Enum.GetValues<ThemeColorRole>();
    private static readonly TypographyRole[] TypographyRoles = Enum.GetValues<TypographyRole>();

    private static readonly string[] AllFields =
    [
        ThemeDocument.ColorsField,
        .. ColorRoles.SelectMany(role => new[] { ColorPath(role), ColorPath(role) + LightSuffix, ColorPath(role) + DarkSuffix }),
        ThemeDocument.TypographyField,
        .. TypographyRoles.SelectMany(TypographyFieldsOf),
        AllowSmallTextField,
        BrandHeightField,
        ControlShapeField,
        DividerWidthField,
        DividerThicknessField,
        DividerColorField,
    ];

    private readonly Dictionary<string, NumberInput> _numbers = new(StringComparer.Ordinal);
    private EditContext? _context;
    private ThemeEditorModel? _model;

    /// <summary>The field path of the small-text switch.</summary>
    public const string AllowSmallTextField = "allowSmallText";

    /// <summary>The field path of the logo height, where the theme reports its rule.</summary>
    public const string BrandHeightField = "header.brandHeight";

    /// <summary>The field path of the corner shape.</summary>
    public const string ControlShapeField = "shape.control";

    /// <summary>The field path of the divider width.</summary>
    public const string DividerWidthField = "shape.dividerWidth";

    /// <summary>The field path of the divider thickness.</summary>
    public const string DividerThicknessField = "shape.dividerThickness";

    /// <summary>The field path of the divider colour.</summary>
    public const string DividerColorField = "shape.dividerColor";

    /// <summary>
    /// Gets every field path the editor shows, in form order: the <c>colors</c> and <c>typography</c> sections, each colour role and its
    /// <c>light</c> and <c>dark</c> value, each typography field (with <c>sizeMax</c>, which the editor checks itself, and <c>color</c>
    /// only on the roles that take one), the small-text switch, the logo height and the shape. Pass it as the known fields of the
    /// form's <see cref="ServerFormSubmission" />.
    /// </summary>
    public static IReadOnlyList<string> Fields => AllFields;

    /// <summary>Gets or sets the form's edit context, which holds the messages of every field path.</summary>
    [CascadingParameter]
    public EditContext? CurrentEditContext { get; set; }

    /// <summary>Gets or sets the theme fields being edited.</summary>
    [Parameter]
    [EditorRequired]
    public ThemeEditorModel Model { get; set; } = default!;

    /// <summary>Gets or sets the prefix of the control ids, such as <c>site-theme</c>; the same prefix the form's submission uses.</summary>
    [Parameter]
    [EditorRequired]
    public string IdPrefix { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the font families a typography role can use, in list order; a role keeps a key that is not offered, shown by the key.
    /// <see langword="null" /> offers only the system font.
    /// </summary>
    [Parameter]
    public IReadOnlyList<ThemeFontOption>? Fonts { get; set; }

    /// <summary>Gets or sets the texts.</summary>
    [Parameter]
    public ThemeEditorTexts Texts { get; set; } = new();

    /// <summary>Gets or sets the callback raised after every change of a field, for example to refresh a preview.</summary>
    [Parameter]
    public EventCallback OnChange { get; set; }

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var number in _numbers.Values)
        {
            number.State.Dispose();
        }

        _numbers.Clear();
        _context = null;
        _model = null;
    }

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">The editor is not inside an <c>EditForm</c>.</exception>
    /// <exception cref="ArgumentNullException"><see cref="Model" /> or <see cref="Texts" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentException"><see cref="IdPrefix" /> is empty or white space.</exception>
    protected override void OnParametersSet()
    {
        var context = CurrentEditContext ?? throw new InvalidOperationException("The theme editor must be inside an EditForm.");
        ArgumentNullException.ThrowIfNull(Model);
        ArgumentNullException.ThrowIfNull(Texts);
        ArgumentException.ThrowIfNullOrWhiteSpace(IdPrefix);
        if (!ReferenceEquals(_context, context) || !ReferenceEquals(_model, Model))
        {
            Dispose();
            _context = context;
            _model = Model;
            foreach (var role in TypographyRoles)
            {
                var path = TypographyPath(role);
                Add(path + SizeSuffix, () => Texts.Size, "1.25");
                Add(path + SizeMaxSuffix, () => Texts.SizeMax, "2.5");
                Add(path + LineHeightSuffix, () => Texts.LineHeight, "1.5");
                Add(path + LetterSpacingSuffix, () => Texts.LetterSpacing, "0.02", mayBeNegative: true);
            }

            Add(BrandHeightField, () => Texts.BrandHeight, "3.5");
            Add(DividerWidthField, () => Texts.DividerWidth, "4");
            Add(DividerThicknessField, () => Texts.DividerThickness, "0.25");
        }

        foreach (var role in TypographyRoles)
        {
            var fields = Model.Typography[role];
            var path = TypographyPath(role);
            Bind(path + SizeSuffix, fields.SizeMin);
            Bind(path + SizeMaxSuffix, fields.SizeMax);
            Bind(path + LineHeightSuffix, fields.LineHeight);
            Bind(path + LetterSpacingSuffix, fields.LetterSpacing);
        }

        Bind(BrandHeightField, Model.BrandHeight);
        Bind(DividerWidthField, Model.DividerWidth);
        Bind(DividerThicknessField, Model.DividerThickness);
    }

    private static string ColorPath(ThemeColorRole role) => $"{ThemeDocument.ColorsField}.{ThemeDocument.RoleName(role)}";

    private static string TypographyPath(TypographyRole role) => $"{ThemeDocument.TypographyField}.{ThemeDocument.RoleName(role)}";

    private static IEnumerable<string> TypographyFieldsOf(TypographyRole role)
    {
        var path = TypographyPath(role);
        string[] suffixes =
        [
            FontKeySuffix, FallbackSuffix, SizeSuffix, SizeMaxSuffix, WeightSuffix, LineHeightSuffix, LetterSpacingSuffix, TransformSuffix,
            ItalicSuffix,
        ];
        return TypographyStyle.TakesColor(role) ? [.. suffixes.Select(suffix => path + suffix), path + ColorSuffix] : suffixes.Select(suffix => path + suffix);
    }

    private static string ItalicValue(bool? italic) => italic switch
    {
        true => ItalicOptionValue,
        false => NormalValue,
        null => string.Empty,
    };

    private static string? Format(decimal? value) => value?.ToString(CultureInfo.InvariantCulture);

    private static string Name(string label) => label.IndexOf(" (", StringComparison.Ordinal) is var open and > 0 ? label[..open] : label;

    private string Id(string field) => FieldIds.For(IdPrefix, field);

    private FieldIdentifier Field(string field) => _context!.Field(field);

    private IEnumerable<ThemeFontOption> FontChoices(string? current)
    {
        var fonts = Fonts ?? [];
        return string.IsNullOrEmpty(current) || fonts.Any(font => font.Key == current) ? fonts : fonts.Append(new ThemeFontOption(current, current));
    }

    private void Add(string path, Func<string> label, string example, bool mayBeNegative = false)
    {
        _numbers[path] = new NumberInput(text => Parse(text, label, example), mayBeNegative ? null : "decimal");
    }

    private void Bind(string path, decimal? value) => _numbers[path].State.Bind(_context!, _context!.Field(path), value);

    private (decimal? Value, string? Error) Parse(string text, Func<string> label, string example)
    {
        var trimmed = text.Trim();
        if (trimmed.Length == 0)
        {
            return (null, null);
        }

        return decimal.TryParse(trimmed.Replace(',', '.'), NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var number)
            ? (number, null)
            : (null, Texts.NotANumber(Name(label()), example));
    }

    private Task Changed() => OnChange.InvokeAsync();

    private Task ChangeNumber(NumberInput number, Action<decimal?> set, ChangeEventArgs change)
    {
        if (!number.State.TrySetText(change.Value as string ?? string.Empty, out var value))
        {
            return Task.CompletedTask;
        }

        set(value);
        return Changed();
    }

    private Task SetFontKey(TypographyFields fields, string? key)
    {
        fields.FontKey = string.IsNullOrEmpty(key) ? null : key;
        return Changed();
    }

    private Task SetItalic(TypographyFields fields, ChangeEventArgs change)
    {
        fields.Italic = (change.Value as string) switch
        {
            ItalicOptionValue => true,
            NormalValue => false,
            _ => null,
        };
        return Changed();
    }

    /// <summary>The text state and keyboard of one number field; the editor disposes its state.</summary>
    /// <param name="parse">Parses the typed text.</param>
    /// <param name="inputMode">The <c>inputmode</c> of the control; <see langword="null" /> for the full keyboard of a value that may be negative.</param>
    private sealed class NumberInput(Func<string, (decimal? Value, string? Error)> parse, string? inputMode)
    {
        /// <summary>Gets the typed text and its parse error.</summary>
        public ParsedTextInputState<decimal?> State { get; } = new(Format, parse);

        /// <summary>Gets the <c>inputmode</c> of the control.</summary>
        public string? InputMode { get; } = inputMode;
    }
}
