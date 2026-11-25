using SharedKernel.Blazor.Site.Theming;

namespace SharedKernel.Blazor.Site.Tests.Theming;

/// <summary>
/// Stored theme text as the first consumer wrote it before the format was versioned (ViajantesTurismo's site theme column, written by its
/// own copy of the document and Web JSON defaults with enums by name and nulls left out), and the themes it describes.
/// </summary>
internal static class ThemeDocumentSamples
{
    /// <summary>The unversioned text of <see cref="SiteTheme.Default" />.</summary>
    public const string UnversionedDefaultText = """
        {"colors":{"primary":{"light":"#1d4ed8","dark":"#93c5fd"},"primaryHover":{"light":"#1e40af","dark":"#60a5fa"},"onPrimary":{"light":"#ffffff","dark":"#111827"},"accent":{"light":"#b45309","dark":"#fbbf24"},"accentHover":{"light":"#92400e","dark":"#f59e0b"},"onAccent":{"light":"#ffffff","dark":"#111827"},"text":{"light":"#1f2937","dark":"#f3f4f6"},"muted":{"light":"#4b5563","dark":"#9ca3af"},"surface":{"light":"#ffffff","dark":"#111827"},"surfaceAlt":{"light":"#f3f4f6","dark":"#1f2937"},"border":{"light":"#d1d5db","dark":"#374151"},"focus":{"light":"#1d4ed8","dark":"#93c5fd"}},"typography":{"h1":{"fallback":"SansSerif","sizeMin":2.125,"sizeMax":3.5,"weight":700,"lineHeight":1.2},"h2":{"fallback":"SansSerif","sizeMin":1.75,"sizeMax":2.5,"weight":700,"lineHeight":1.2},"h3":{"fallback":"SansSerif","sizeMin":1.375,"sizeMax":1.875,"weight":700,"lineHeight":1.2},"h4":{"fallback":"SansSerif","sizeMin":1.125,"sizeMax":1.375,"weight":700,"lineHeight":1.2},"h5":{"fallback":"SansSerif","sizeMin":1,"sizeMax":1.125,"weight":700,"lineHeight":1.2},"h6":{"fallback":"SansSerif","sizeMin":0.875,"sizeMax":0.9375,"weight":700,"lineHeight":1.2},"body":{"fallback":"SansSerif","sizeMin":1,"sizeMax":1.125,"weight":400,"lineHeight":1.6},"small":{"sizeMin":0.875,"sizeMax":0.9375},"button":{"weight":600},"cardTitle":{"fallback":"SansSerif","sizeMin":1.125,"sizeMax":1.375,"weight":700,"lineHeight":1.2},"brand":{"sizeMin":1.125,"sizeMax":1.375,"lineHeight":1.2}},"fonts":[],"allowSmallText":false,"header":{"brandHeight":2.5},"shape":{"control":"Rounded","dividerWidth":4,"dividerThickness":0.25,"dividerColor":"Accent"}}
        """;

    /// <summary>The unversioned text of <see cref="Rich" />.</summary>
    public const string UnversionedRichText = """
        {"colors":{"primary":{"light":"#1f4399","dark":"#8fa8e6"},"onPrimary":{"light":"#ffffff","dark":"#111827"},"accent":{"light":"#b45309","dark":"#fbbf24"},"accentHover":{"light":"#92400e","dark":"#f59e0b"},"onAccent":{"light":"#ffffff","dark":"#111827"},"text":{"light":"#1f2937","dark":"#f3f4f6"},"muted":{"light":"#4b5563","dark":"#9ca3af"},"surface":{"light":"#ffffff","dark":"#111827"},"surfaceAlt":{"light":"#f3f4f6","dark":"#1f2937"},"border":{"light":"#d1d5db","dark":"#374151"},"focus":{"light":"#1f4399","dark":"#8fa8e6"}},"typography":{"h1":{"fallback":"SansSerif","sizeMin":2.25,"sizeMax":3.25,"weight":700,"lineHeight":1.2,"letterSpacing":-0.02},"h2":{"fallback":"SansSerif","sizeMin":1.75,"sizeMax":2.5,"weight":700,"lineHeight":1.2,"transform":"Uppercase","color":"Primary"},"h3":{"fallback":"SansSerif","sizeMin":1.375,"sizeMax":1.875,"weight":700,"lineHeight":1.2},"h4":{"fallback":"SansSerif","sizeMin":1.125,"sizeMax":1.375,"weight":700,"lineHeight":1.2},"h5":{"fallback":"SansSerif","sizeMin":1,"sizeMax":1.125,"weight":700,"lineHeight":1.2},"h6":{"fallback":"SansSerif","sizeMin":0.875,"sizeMax":0.9375,"weight":700,"lineHeight":1.2},"body":{"fontKey":"noto-sans","fallback":"Serif","sizeMin":1,"sizeMax":1.125,"weight":400,"lineHeight":1.6},"small":{"sizeMin":0.875,"sizeMax":0.9375,"italic":true},"button":{"weight":600},"nav":{"weight":600},"cardTitle":{"fallback":"SansSerif","sizeMin":1.25,"sizeMax":1.375,"weight":700,"lineHeight":1.2,"color":"Primary"},"brand":{"sizeMin":1.125,"sizeMax":1.375,"lineHeight":1.2}},"fonts":[{"key":"noto-sans","family":"Noto Sans","faces":[{"weight":400,"style":"Normal","src":"/media/0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef.woff2"},{"weight":700,"style":"Italic","src":"/media/fedcba9876543210fedcba9876543210fedcba9876543210fedcba9876543210.woff2"}]}],"allowSmallText":true,"header":{"brandHeight":3.25},"shape":{"control":"Pill","dividerWidth":4.375,"dividerThickness":0.1875,"dividerColor":"Primary"}}
        """;

    /// <summary>The text of <see cref="Rich" /> in the first stored shape: no header, no shape and no heading colours.</summary>
    public const string FirstShapeText = """
        {"colors":{"primary":{"light":"#1f4399","dark":"#8fa8e6"},"onPrimary":{"light":"#ffffff","dark":"#111827"},"accent":{"light":"#b45309","dark":"#fbbf24"},"accentHover":{"light":"#92400e","dark":"#f59e0b"},"onAccent":{"light":"#ffffff","dark":"#111827"},"text":{"light":"#1f2937","dark":"#f3f4f6"},"muted":{"light":"#4b5563","dark":"#9ca3af"},"surface":{"light":"#ffffff","dark":"#111827"},"surfaceAlt":{"light":"#f3f4f6","dark":"#1f2937"},"border":{"light":"#d1d5db","dark":"#374151"},"focus":{"light":"#1f4399","dark":"#8fa8e6"}},"typography":{"h1":{"fallback":"SansSerif","sizeMin":2.25,"sizeMax":3.25,"weight":700,"lineHeight":1.2,"letterSpacing":-0.02},"h2":{"fallback":"SansSerif","sizeMin":1.75,"sizeMax":2.5,"weight":700,"lineHeight":1.2,"transform":"Uppercase"},"h3":{"fallback":"SansSerif","sizeMin":1.375,"sizeMax":1.875,"weight":700,"lineHeight":1.2},"h4":{"fallback":"SansSerif","sizeMin":1.125,"sizeMax":1.375,"weight":700,"lineHeight":1.2},"h5":{"fallback":"SansSerif","sizeMin":1,"sizeMax":1.125,"weight":700,"lineHeight":1.2},"h6":{"fallback":"SansSerif","sizeMin":0.875,"sizeMax":0.9375,"weight":700,"lineHeight":1.2},"body":{"fontKey":"noto-sans","fallback":"Serif","sizeMin":1,"sizeMax":1.125,"weight":400,"lineHeight":1.6},"small":{"sizeMin":0.875,"sizeMax":0.9375,"italic":true},"button":{"weight":600},"nav":{"weight":600},"cardTitle":{"fallback":"SansSerif","sizeMin":1.25,"sizeMax":1.375,"weight":700,"lineHeight":1.2},"brand":{"sizeMin":1.125,"sizeMax":1.375,"lineHeight":1.2}},"fonts":[{"key":"noto-sans","family":"Noto Sans","faces":[{"weight":400,"style":"Normal","src":"/media/0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef.woff2"},{"weight":700,"style":"Italic","src":"/media/fedcba9876543210fedcba9876543210fedcba9876543210fedcba9876543210.woff2"}]}],"allowSmallText":true}
        """;

    /// <summary>The text of <see cref="Rich" /> in the second stored shape: a header, but no shape and no heading colours.</summary>
    public const string HeaderShapeText = """
        {"colors":{"primary":{"light":"#1f4399","dark":"#8fa8e6"},"onPrimary":{"light":"#ffffff","dark":"#111827"},"accent":{"light":"#b45309","dark":"#fbbf24"},"accentHover":{"light":"#92400e","dark":"#f59e0b"},"onAccent":{"light":"#ffffff","dark":"#111827"},"text":{"light":"#1f2937","dark":"#f3f4f6"},"muted":{"light":"#4b5563","dark":"#9ca3af"},"surface":{"light":"#ffffff","dark":"#111827"},"surfaceAlt":{"light":"#f3f4f6","dark":"#1f2937"},"border":{"light":"#d1d5db","dark":"#374151"},"focus":{"light":"#1f4399","dark":"#8fa8e6"}},"typography":{"h1":{"fallback":"SansSerif","sizeMin":2.25,"sizeMax":3.25,"weight":700,"lineHeight":1.2,"letterSpacing":-0.02},"h2":{"fallback":"SansSerif","sizeMin":1.75,"sizeMax":2.5,"weight":700,"lineHeight":1.2,"transform":"Uppercase"},"h3":{"fallback":"SansSerif","sizeMin":1.375,"sizeMax":1.875,"weight":700,"lineHeight":1.2},"h4":{"fallback":"SansSerif","sizeMin":1.125,"sizeMax":1.375,"weight":700,"lineHeight":1.2},"h5":{"fallback":"SansSerif","sizeMin":1,"sizeMax":1.125,"weight":700,"lineHeight":1.2},"h6":{"fallback":"SansSerif","sizeMin":0.875,"sizeMax":0.9375,"weight":700,"lineHeight":1.2},"body":{"fontKey":"noto-sans","fallback":"Serif","sizeMin":1,"sizeMax":1.125,"weight":400,"lineHeight":1.6},"small":{"sizeMin":0.875,"sizeMax":0.9375,"italic":true},"button":{"weight":600},"nav":{"weight":600},"cardTitle":{"fallback":"SansSerif","sizeMin":1.25,"sizeMax":1.375,"weight":700,"lineHeight":1.2},"brand":{"sizeMin":1.125,"sizeMax":1.375,"lineHeight":1.2}},"fonts":[{"key":"noto-sans","family":"Noto Sans","faces":[{"weight":400,"style":"Normal","src":"/media/0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef.woff2"},{"weight":700,"style":"Italic","src":"/media/fedcba9876543210fedcba9876543210fedcba9876543210fedcba9876543210.woff2"}]}],"allowSmallText":true,"header":{"brandHeight":3.25}}
        """;

    /// <summary>
    /// A valid theme that uses every kind of value: a derived and a given hover, fluid sizes, spacing, case, italics, a fallback, a font
    /// with two faces, small text, a brand height, a shape other than the default and heading colours.
    /// </summary>
    /// <returns>The theme.</returns>
    public static SiteTheme Rich()
    {
        var model = ThemeEditorModel.FromTheme(SiteTheme.Default);
        Set(model, ThemeColorRole.Primary, "#1f4399", "#8fa8e6");
        Set(model, ThemeColorRole.PrimaryHover, null, null);
        Set(model, ThemeColorRole.Focus, "#1f4399", "#8fa8e6");
        model.Typography[TypographyRole.H1].SizeMin = 2.25m;
        model.Typography[TypographyRole.H1].SizeMax = 3.25m;
        model.Typography[TypographyRole.H1].LetterSpacing = -0.02m;
        model.Typography[TypographyRole.H2].Transform = ThemeTextTransform.Uppercase;
        model.Typography[TypographyRole.H2].Color = ThemeColorRole.Primary;
        model.Typography[TypographyRole.Small].Italic = true;
        model.Typography[TypographyRole.Body].Fallback = GenericFontFamily.Serif;
        model.Typography[TypographyRole.Body].LineHeight = 1.6m;
        model.Typography[TypographyRole.Body].FontKey = "noto-sans";
        model.Typography[TypographyRole.Nav].Weight = 600;
        model.Typography[TypographyRole.CardTitle].SizeMin = 1.25m;
        model.Typography[TypographyRole.CardTitle].Color = ThemeColorRole.Primary;
        model.AllowSmallText = true;
        model.BrandHeight = 3.25m;
        model.ControlShape = ControlShape.Pill;
        model.DividerWidth = 4.375m;
        model.DividerThickness = 0.1875m;
        model.DividerColor = ThemeColorRole.Primary;
        var font = new ThemeFontFields { Key = "noto-sans", Family = "Noto Sans" };
        font.Faces.Add(new ThemeFontFaceFields { Weight = 400, Style = ThemeFontStyle.Normal, Src = "/media/0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef.woff2" });
        font.Faces.Add(new ThemeFontFaceFields { Weight = 700, Style = ThemeFontStyle.Italic, Src = "/media/fedcba9876543210fedcba9876543210fedcba9876543210fedcba9876543210.woff2" });
        model.Fonts.Add(font);
        return model.ToTheme().ShouldBeSuccess();
    }

    private static void Set(ThemeEditorModel model, ThemeColorRole role, string? light, string? dark)
    {
        model.Colors[role].Light = light;
        model.Colors[role].Dark = dark;
    }
}
