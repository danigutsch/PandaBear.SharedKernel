namespace SharedKernel.Blazor.Site.Theming;

/// <summary>One colour token of a <see cref="ThemeDocument" />: its light and dark value as <c>#rrggbb</c> or <c>#rgb</c>.</summary>
/// <param name="Light">The light-scheme value.</param>
/// <param name="Dark">The dark-scheme value.</param>
public sealed record ThemeColorDocument(string? Light, string? Dark);
