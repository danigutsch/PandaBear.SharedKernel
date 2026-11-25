using System.Globalization;
using System.Text.Json.Serialization;
using SharedKernel.Results;

namespace SharedKernel.Blazor.Site.Theming;

/// <summary>
/// A site theme as plain, versioned data: the shape a host stores it in (one JSON value, written and read by
/// <see cref="ThemeDocumentJson" />) or embeds in an import file. It maps to <see cref="ThemeEditorModel" /> and from there to
/// <see cref="SiteTheme" />, so every rule and every field path (<c>colors.primary.light</c>, <c>typography.h1.size</c>,
/// <c>fonts[0].faces[0].src</c>) is the model's. Colours are keyed by role in camel case (<c>primary</c>, <c>surfaceAlt</c>), typography
/// by role (<c>h1</c> to <c>h6</c>, <c>body</c>, <c>small</c>, <c>link</c>, <c>button</c>, <c>nav</c>, <c>cardTitle</c>, <c>brand</c>).
/// A hover colour that is missing is derived; a typography role, a header or a shape value that is missing keeps
/// <see cref="SiteTheme.Default" />'s.
/// </summary>
/// <param name="Colors">The colour tokens by role.</param>
/// <param name="Typography">The typography by role.</param>
/// <param name="Fonts">The self-hosted fonts.</param>
/// <param name="AllowSmallText">Whether body text below 1rem and other text below 0.875rem is allowed.</param>
/// <param name="Header">The header: the logo height (<c>header.brandHeight</c>); without it the default height.</param>
/// <param name="Shape">The corner shape and the divider (<c>shape.control</c>, <c>shape.dividerWidth</c>, ...); without it the default shape.</param>
public sealed record ThemeDocument(
    IReadOnlyDictionary<string, ThemeColorDocument>? Colors,
    IReadOnlyDictionary<string, TypographyDocument>? Typography = null,
    IReadOnlyList<ThemeFontDocument>? Fonts = null,
    bool AllowSmallText = false,
    ThemeHeaderDocument? Header = null,
    ThemeShapeDocument? Shape = null)
{
    /// <summary>
    /// The format version this library writes, and the newest it reads. Version 1 is the first format; a document without a
    /// <c>version</c> member is version 1, so a document stored before the format was versioned reads unchanged.
    /// </summary>
    public const int CurrentVersion = 1;

    /// <summary>The field of an unsupported format version.</summary>
    public const string VersionField = "version";

    /// <summary>The field of an unknown colour role.</summary>
    public const string ColorsField = "colors";

    /// <summary>The field of an unknown typography role.</summary>
    public const string TypographyField = "typography";

    private static readonly Dictionary<string, ThemeColorRole> ColorRoles =
        Enum.GetValues<ThemeColorRole>().ToDictionary(RoleName, StringComparer.Ordinal);

    private static readonly Dictionary<string, TypographyRole> TypographyRoles =
        Enum.GetValues<TypographyRole>().ToDictionary(RoleName, StringComparer.Ordinal);

    /// <summary>
    /// Gets the format version of the document, written first. <see cref="CurrentVersion" /> unless set; a document read without a
    /// <c>version</c> member is version 1.
    /// </summary>
    [JsonPropertyOrder(-1)]
    public int Version { get; init; } = CurrentVersion;

    /// <summary>Gets the name of a colour role in the document and in its field paths, such as <c>surfaceAlt</c>.</summary>
    /// <param name="role">The role.</param>
    /// <returns>The name.</returns>
    public static string RoleName(ThemeColorRole role) => ThemeNames.Field(role);

    /// <summary>Gets the name of a typography role in the document and in its field paths, such as <c>h1</c> or <c>cardTitle</c>.</summary>
    /// <param name="role">The role.</param>
    /// <returns>The name.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="role" /> is not a defined role.</exception>
    public static string RoleName(TypographyRole role) => ThemeNames.Field(role);

    /// <summary>Describes a theme; a derived hover colour and an unset typography role are left out.</summary>
    /// <param name="theme">The theme.</param>
    /// <returns>The document, at <see cref="CurrentVersion" />.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="theme" /> is <see langword="null" />.</exception>
    public static ThemeDocument From(SiteTheme theme) => From(ThemeEditorModel.FromTheme(theme));

    /// <summary>
    /// Describes the fields of a theme editor as they are, valid or not: a colour role with both values empty, a typography role with
    /// every value empty and a shape with every value empty are left out, and roles are in enum order.
    /// </summary>
    /// <param name="model">The editor model.</param>
    /// <returns>The document, at <see cref="CurrentVersion" />.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="model" /> is <see langword="null" />.</exception>
    public static ThemeDocument From(ThemeEditorModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        var colors = model.Colors
            .Where(pair => pair.Value.Light is not null || pair.Value.Dark is not null)
            .OrderBy(pair => pair.Key)
            .ToDictionary(pair => RoleName(pair.Key), pair => new ThemeColorDocument(pair.Value.Light, pair.Value.Dark), StringComparer.Ordinal);
        var typography = model.Typography
            .Select(pair => (pair.Key, Style: new TypographyDocument(
                pair.Value.FontKey,
                pair.Value.Fallback,
                pair.Value.SizeMin,
                pair.Value.SizeMax,
                pair.Value.Weight,
                pair.Value.LineHeight,
                pair.Value.LetterSpacing,
                pair.Value.Transform,
                pair.Value.Italic,
                pair.Value.Color)))
            .Where(pair => pair.Style != new TypographyDocument())
            .OrderBy(pair => pair.Key)
            .ToDictionary(pair => RoleName(pair.Key), pair => pair.Style, StringComparer.Ordinal);
        var fonts = model.Fonts
            .Select(font => new ThemeFontDocument(font.Key, font.Family, [.. font.Faces.Select(face => new ThemeFontFaceDocument(face.Weight, face.Style, face.Src))]))
            .ToList();
        var header = model.BrandHeight is { } brandHeight ? new ThemeHeaderDocument(brandHeight) : null;
        var shape = new ThemeShapeDocument(model.ControlShape, model.DividerWidth, model.DividerThickness, model.DividerColor);
        return new ThemeDocument(colors, typography, fonts, model.AllowSmallText, header, shape == new ThemeShapeDocument() ? null : shape);
    }

    /// <summary>Fills a theme editor with this document.</summary>
    /// <returns>
    /// The model, or an error on <see cref="VersionField" /> for a format version this library does not read, or on
    /// <see cref="ColorsField" /> or <see cref="TypographyField" /> for a role name that does not exist.
    /// </returns>
    public Result<ThemeEditorModel> ToEditorModel()
    {
        if (!IsSupportedVersion)
        {
            return Result.Invalid<ThemeEditorModel>(
                "The theme document has a format version this library does not read.",
                VersionField,
                string.Create(CultureInfo.InvariantCulture, $"The theme format version {Version} is not supported. Use version 1 to {CurrentVersion}."));
        }

        var model = new ThemeEditorModel
        {
            AllowSmallText = AllowSmallText,
            BrandHeight = Header?.BrandHeight,
            ControlShape = Shape?.Control,
            DividerWidth = Shape?.DividerWidth,
            DividerThickness = Shape?.DividerThickness,
            DividerColor = Shape?.DividerColor,
        };
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        foreach (var (name, color) in Colors ?? new Dictionary<string, ThemeColorDocument>())
        {
            if (!ColorRoles.TryGetValue(name, out var role))
            {
                errors[ColorsField] = [$"A color role is not known. Use {string.Join(", ", ColorRoles.Keys)}."];
                continue;
            }

            model.Colors[role].Light = color?.Light;
            model.Colors[role].Dark = color?.Dark;
        }

        foreach (var (name, style) in Typography ?? new Dictionary<string, TypographyDocument>())
        {
            if (!TypographyRoles.TryGetValue(name, out var role))
            {
                errors[TypographyField] = [$"A typography role is not known. Use {string.Join(", ", TypographyRoles.Keys)}."];
                continue;
            }

            if (style is null)
            {
                continue;
            }

            var fields = model.Typography[role];
            fields.FontKey = style.FontKey;
            fields.Fallback = style.Fallback;
            fields.SizeMin = style.SizeMin;
            fields.SizeMax = style.SizeMax;
            fields.Weight = style.Weight;
            fields.LineHeight = style.LineHeight;
            fields.LetterSpacing = style.LetterSpacing;
            fields.Transform = style.Transform;
            fields.Italic = style.Italic;
            fields.Color = style.Color;
        }

        foreach (var font in Fonts ?? [])
        {
            var fields = new ThemeFontFields { Key = font?.Key, Family = font?.Family };
            foreach (var face in font?.Faces ?? [])
            {
                fields.Faces.Add(new ThemeFontFaceFields { Weight = face?.Weight, Style = face?.Style ?? ThemeFontStyle.Normal, Src = face?.Src });
            }

            model.Fonts.Add(fields);
        }

        return errors.Count > 0 ? Result.Invalid<ThemeEditorModel>("The theme names an unknown role.", errors) : Result.Ok(model);
    }

    /// <summary>Builds the theme through <see cref="SiteTheme.Create" />, which enforces every rule.</summary>
    /// <param name="colorLabel">
    /// The names a contrast message gives the colour roles, as in <see cref="ThemeEditorModel.ToTheme" />; without it, or for a role it
    /// gives no name, the message uses the CSS name (<c>surface-alt</c>). Field paths do not change.
    /// </param>
    /// <returns>The theme, or every error by field path.</returns>
    public Result<SiteTheme> ToTheme(Func<ThemeColorRole, string?>? colorLabel = null)
    {
        var model = ToEditorModel();
        return model.IsSuccess ? model.Value.ToTheme(colorLabel) : model.ConvertError<ThemeEditorModel, SiteTheme>();
    }

    /// <summary>
    /// Restores a stored theme. It met every rule when it was stored, so only what rendering needs is checked again
    /// (<see cref="ThemeEditorModel.ToDraft" />): a theme stored under older rules still loads.
    /// </summary>
    /// <returns>The theme.</returns>
    /// <exception cref="InvalidOperationException">The document has a format version this library does not read, or cannot be read as a theme.</exception>
    public SiteTheme Restore()
    {
        if (!IsSupportedVersion)
        {
            throw new InvalidOperationException(string.Create(
                CultureInfo.InvariantCulture,
                $"The stored site theme has format version {Version}; this library reads version 1 to {CurrentVersion}."));
        }

        var model = ToEditorModel();
        var draft = model.IsSuccess ? model.Value.ToDraft() : model.ConvertError<ThemeEditorModel, SiteTheme>();
        return draft.IsSuccess ? draft.Value : throw new InvalidOperationException("The stored site theme cannot be read.");
    }

    private bool IsSupportedVersion => Version is >= 1 and <= CurrentVersion;
}
