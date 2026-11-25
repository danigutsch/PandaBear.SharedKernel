namespace SharedKernel.Blazor.Site.Editing;

/// <summary>A font family the theme editor offers for a typography role, such as a family of the host's font library.</summary>
/// <param name="Key">The font key typography roles reference, such as <c>noto-sans</c>.</param>
/// <param name="Family">The family name shown in the list, such as <c>Noto Sans</c>.</param>
public sealed record ThemeFontOption(string Key, string Family);
