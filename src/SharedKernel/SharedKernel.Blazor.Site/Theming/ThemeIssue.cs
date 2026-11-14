namespace PandaBear.Blazor.Site.Theming;

/// <summary>A rule finding about a theme, on the field path an editor links to, such as <c>typography.h1.size</c>.</summary>
/// <param name="Field">The field path.</param>
/// <param name="Message">The message; it never repeats free text from the theme.</param>
public sealed record ThemeIssue(string Field, string Message);
