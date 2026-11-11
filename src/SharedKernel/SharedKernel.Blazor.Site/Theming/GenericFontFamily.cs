namespace PandaBear.Blazor.Site.Theming;

/// <summary>The system font stack used when a self-hosted font is not set, still loading or missing a glyph.</summary>
public enum GenericFontFamily
{
    /// <summary><c>system-ui, -apple-system, "Segoe UI", Roboto, "Helvetica Neue", Arial, sans-serif</c>.</summary>
    SansSerif = 0,

    /// <summary><c>Georgia, "Times New Roman", Times, serif</c>.</summary>
    Serif = 1,

    /// <summary><c>ui-monospace, SFMono-Regular, Menlo, Consolas, "Liberation Mono", monospace</c>.</summary>
    Monospace = 2,
}
