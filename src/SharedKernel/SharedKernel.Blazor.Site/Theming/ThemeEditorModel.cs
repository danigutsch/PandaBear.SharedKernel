using System.Collections.ObjectModel;
using PandaBear.Results;

namespace PandaBear.Blazor.Site.Theming;

/// <summary>
/// The mutable form model of a theme editor: plain fields that bind to inputs, mapped to the theme value objects with every
/// error on its field path, such as <c>colors.primary.light</c>, <c>typography.h1.size</c> or <c>fonts[0].faces[1].src</c>.
/// It is a building block for an administration page, not the page itself.
/// </summary>
public sealed class ThemeEditorModel
{
    /// <summary>Creates an empty model with fields for every colour and typography role.</summary>
    public ThemeEditorModel()
    {
        Colors = new ReadOnlyDictionary<ThemeColorRole, ThemeColorFields>(Enum.GetValues<ThemeColorRole>().ToDictionary(role => role, _ => new ThemeColorFields()));
        Typography = new ReadOnlyDictionary<TypographyRole, TypographyFields>(Enum.GetValues<TypographyRole>().ToDictionary(role => role, _ => new TypographyFields()));
    }

    /// <summary>Gets the colour fields by role.</summary>
    public IReadOnlyDictionary<ThemeColorRole, ThemeColorFields> Colors { get; }

    /// <summary>Gets the typography fields by role.</summary>
    public IReadOnlyDictionary<TypographyRole, TypographyFields> Typography { get; }

    /// <summary>Gets the fonts.</summary>
    public Collection<ThemeFontFields> Fonts { get; } = [];

    /// <summary>Gets or sets the brand height in rem; empty means <see cref="SiteTheme.DefaultBrandHeight" />.</summary>
    public decimal? BrandHeight { get; set; }

    /// <summary>Gets or sets the corner shape; empty means <see cref="SiteShape.Default" />'s.</summary>
    public ControlShape? ControlShape { get; set; }

    /// <summary>Gets or sets the divider width in rem; empty means <see cref="SiteShape.DefaultDividerWidth" />.</summary>
    public decimal? DividerWidth { get; set; }

    /// <summary>Gets or sets the divider thickness in rem; empty means <see cref="SiteShape.DefaultDividerThickness" />.</summary>
    public decimal? DividerThickness { get; set; }

    /// <summary>Gets or sets the divider colour; empty means <see cref="SiteShape.Default" />'s.</summary>
    public ThemeColorRole? DividerColor { get; set; }

    /// <summary>Gets or sets whether body text below 1rem and other text below 0.875rem is allowed.</summary>
    public bool AllowSmallText { get; set; }

    /// <summary>Fills a model from a theme; a derived hover colour leaves its fields empty.</summary>
    /// <param name="theme">The theme.</param>
    /// <returns>The model.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="theme" /> is <see langword="null" />.</exception>
    public static ThemeEditorModel FromTheme(SiteTheme theme)
    {
        ArgumentNullException.ThrowIfNull(theme);
        var model = new ThemeEditorModel
        {
            AllowSmallText = theme.AllowsSmallText,
            BrandHeight = theme.BrandHeight,
            ControlShape = theme.Shape.Control,
            DividerWidth = theme.Shape.DividerWidth,
            DividerThickness = theme.Shape.DividerThickness,
            DividerColor = theme.Shape.DividerColor,
        };
        foreach (var (role, fields) in model.Colors)
        {
            if (!theme.Colors.IsDerived(role))
            {
                fields.Light = theme.Colors[role].Light.Value;
                fields.Dark = theme.Colors[role].Dark.Value;
            }
        }

        foreach (var (role, fields) in model.Typography)
        {
            var style = theme.Typography[role];
            fields.FontKey = style.FontKey;
            fields.Fallback = style.Fallback;
            fields.SizeMin = style.Size?.Min;
            fields.SizeMax = style.Size?.Max;
            fields.Weight = style.Weight;
            fields.LineHeight = style.LineHeight;
            fields.LetterSpacing = style.LetterSpacing;
            fields.Transform = style.Transform;
            fields.Italic = style.Italic;
            fields.Color = style.Color;
        }

        foreach (var font in theme.Fonts)
        {
            var fields = new ThemeFontFields { Key = font.Key, Family = font.Family };
            foreach (var face in font.Faces)
            {
                fields.Faces.Add(new ThemeFontFaceFields { Weight = face.Weight, Style = face.Style, Src = face.Src.Value });
            }

            model.Fonts.Add(fields);
        }

        return model;
    }

    /// <summary>Builds a draft for <see cref="ThemePreview" />: the fields must be well formed, the readability rules are not enforced.</summary>
    /// <returns>The draft, or every field error at once.</returns>
    public Result<SiteTheme> ToDraft() =>
        Build((colors, typography, fonts, allowSmallText, brandHeight, shape) => SiteTheme.CreateDraft(colors, typography, fonts, allowSmallText, brandHeight, shape));

    /// <summary>Builds a theme that meets every rule, ready to store.</summary>
    /// <param name="colorLabel">The names of the colour roles in contrast messages, as in <see cref="SiteTheme.Review" />.</param>
    /// <returns>The theme, or every field and rule error at once.</returns>
    public Result<SiteTheme> ToTheme(Func<ThemeColorRole, string?>? colorLabel = null) =>
        Build((colors, typography, fonts, allowSmallText, brandHeight, shape) => SiteTheme.Create(colors, typography, fonts, allowSmallText, brandHeight, colorLabel, shape));

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private Result<SiteTheme> Build(Func<SiteColors, SiteTypography, IEnumerable<ThemeFont>, bool, decimal, SiteShape, Result<SiteTheme>> create)
    {
        var errors = new ThemeErrors();
        var colors = BuildColors(errors);
        var typography = BuildTypography(errors);
        var fonts = BuildFonts(errors);
        var shape = SiteShape.Create(
            ControlShape ?? SiteShape.Default.Control,
            DividerWidth ?? SiteShape.DefaultDividerWidth,
            DividerThickness ?? SiteShape.DefaultDividerThickness,
            DividerColor ?? SiteShape.Default.DividerColor);
        errors.Merge("shape", shape.WithoutValue());
        return errors.HasErrors || colors is null
            ? errors.ToResult<SiteTheme>("The site theme has invalid fields.")
            : create(colors, typography, fonts, AllowSmallText, BrandHeight ?? SiteTheme.DefaultBrandHeight, shape.Value);
    }

    private SiteColors? BuildColors(ThemeErrors errors)
    {
        var built = new Dictionary<ThemeColorRole, ThemeColor?>();
        foreach (var (role, fields) in Colors)
        {
            var path = "colors." + ThemeNames.Field(role);
            var light = Blank(fields.Light);
            var dark = Blank(fields.Dark);
            if (role is ThemeColorRole.PrimaryHover or ThemeColorRole.AccentHover && light is null && dark is null)
            {
                built[role] = null;
                continue;
            }

            var lightColor = Color(errors, path + ".light", light);
            var darkColor = Color(errors, path + ".dark", dark);
            built[role] = lightColor is null || darkColor is null ? null : ThemeColor.Create(lightColor, darkColor);
        }

        if (errors.HasErrors)
        {
            return null;
        }

        return SiteColors.Create(
            built[ThemeColorRole.Primary]!,
            built[ThemeColorRole.OnPrimary]!,
            built[ThemeColorRole.Accent]!,
            built[ThemeColorRole.OnAccent]!,
            built[ThemeColorRole.Text]!,
            built[ThemeColorRole.Muted]!,
            built[ThemeColorRole.Surface]!,
            built[ThemeColorRole.SurfaceAlt]!,
            built[ThemeColorRole.Border]!,
            built[ThemeColorRole.Focus]!,
            built[ThemeColorRole.PrimaryHover],
            built[ThemeColorRole.AccentHover]);
    }

    private static CssColor? Color(ThemeErrors errors, string path, string? value)
    {
        var color = CssColor.Create(value);
        if (color.TryGetValue(out var created))
        {
            return created;
        }

        errors.Add(path, color.ErrorDetails?.Detail ?? "The colour is invalid.");
        return null;
    }

    private SiteTypography BuildTypography(ThemeErrors errors)
    {
        var styles = new Dictionary<TypographyRole, TypographyStyle>();
        foreach (var (role, fields) in Typography)
        {
            var path = "typography." + ThemeNames.Field(role);
            FontSize? size = null;
            if (fields.SizeMin is { } min)
            {
                var created = fields.SizeMax is { } max && max != min ? FontSize.Fluid(min, max) : FontSize.Fixed(min);
                errors.Merge(path, created.WithoutValue());
                size = created.IsSuccess ? created.Value : null;
            }
            else if (fields.SizeMax is not null)
            {
                errors.Add(path + ".size", "Enter the smallest size too, or only a fixed size.");
            }

            var style = TypographyStyle.Create(
                Blank(fields.FontKey),
                fields.Fallback,
                size,
                fields.Weight,
                fields.LineHeight,
                fields.LetterSpacing,
                fields.Transform,
                fields.Italic,
                fields.Color);
            errors.Merge(path, style.WithoutValue());
            styles[role] = style.IsSuccess ? style.Value : TypographyStyle.Inherit;
        }

        return SiteTypography.Create(styles);
    }

    private List<ThemeFont> BuildFonts(ThemeErrors errors)
    {
        var fonts = new List<ThemeFont>();
        for (var index = 0; index < Fonts.Count; index++)
        {
            var fields = Fonts[index];
            var path = $"fonts[{index}]";
            var faces = new List<ThemeFontFace>();
            var faceErrors = false;
            for (var faceIndex = 0; faceIndex < fields.Faces.Count; faceIndex++)
            {
                var face = Face(errors, $"{path}.faces[{faceIndex}]", fields.Faces[faceIndex]);
                if (face is null)
                {
                    faceErrors = true;
                }
                else
                {
                    faces.Add(face);
                }
            }

            var font = ThemeFont.Create(Blank(fields.Key), fields.Family, faces);
            foreach (var (field, messages) in font.WithoutValue().ValidationErrors())
            {
                if (!(faceErrors && field == "faces"))
                {
                    Array.ForEach(messages, message => errors.Add(path + "." + field, message));
                }
            }

            if (font.IsSuccess)
            {
                fonts.Add(font.Value);
            }
        }

        return fonts;
    }

    private static ThemeFontFace? Face(ThemeErrors errors, string path, ThemeFontFaceFields fields)
    {
        if (fields.Weight is null)
        {
            errors.Add(path + ".weight", ThemeFont.WeightMessage);
        }

        var src = LinkTarget.Create(fields.Src);
        if (src.IsFailure)
        {
            errors.Add(path + ".src", "A font file must be a root-relative or https address of a .woff2 file.");
        }

        if (fields.Weight is not { } weight || !src.TryGetValue(out var target))
        {
            return null;
        }

        var face = ThemeFontFace.Create(weight, fields.Style, target);
        errors.Merge(path, face.WithoutValue());
        return face.IsSuccess ? face.Value : null;
    }
}
