namespace SharedKernel.Blazor.Site.Theming;

/// <summary>One file of a font in a <see cref="ThemeDocument" />.</summary>
/// <param name="Weight">The weight.</param>
/// <param name="Style">The style.</param>
/// <param name="Src">The address of the <c>.woff2</c> file, such as <c>/media/font.woff2</c>.</param>
public sealed record ThemeFontFaceDocument(int? Weight, ThemeFontStyle Style, string? Src);
