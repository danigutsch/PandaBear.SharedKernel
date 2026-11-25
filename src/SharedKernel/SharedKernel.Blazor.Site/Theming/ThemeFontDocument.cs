namespace SharedKernel.Blazor.Site.Theming;

/// <summary>A self-hosted font of a <see cref="ThemeDocument" />.</summary>
/// <param name="Key">The key typography roles reference.</param>
/// <param name="Family">The family name.</param>
/// <param name="Faces">The font files.</param>
public sealed record ThemeFontDocument(string? Key, string? Family, IReadOnlyList<ThemeFontFaceDocument>? Faces);
