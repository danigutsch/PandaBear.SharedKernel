namespace PandaBear.Blazor.Site.Theming;

/// <summary>The CSS names and field-path names of the theme roles.</summary>
internal static class ThemeNames
{
    /// <summary>Gets the CSS name of a colour role, such as <c>surface-alt</c>.</summary>
    /// <param name="role">The role.</param>
    /// <returns>The name.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="role" /> is not a defined role.</exception>
    public static string Css(ThemeColorRole role) => role switch
    {
        ThemeColorRole.Primary => "primary",
        ThemeColorRole.PrimaryHover => "primary-hover",
        ThemeColorRole.OnPrimary => "on-primary",
        ThemeColorRole.Accent => "accent",
        ThemeColorRole.AccentHover => "accent-hover",
        ThemeColorRole.OnAccent => "on-accent",
        ThemeColorRole.Text => "text",
        ThemeColorRole.Muted => "muted",
        ThemeColorRole.Surface => "surface",
        ThemeColorRole.SurfaceAlt => "surface-alt",
        ThemeColorRole.Border => "border",
        ThemeColorRole.Focus => "focus",
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, "The colour role is not defined."),
    };

    /// <summary>Gets the name a reader sees for a colour role: the host's label, or the CSS name when the host gives none.</summary>
    /// <param name="role">The role.</param>
    /// <param name="colorLabel">The host's labels; <see langword="null" />, or a <see langword="null" /> or white-space label, keeps the CSS name.</param>
    /// <returns>The name, such as <c>Card background</c> or <c>surface-alt</c>.</returns>
    public static string Label(ThemeColorRole role, Func<ThemeColorRole, string?>? colorLabel) =>
        colorLabel?.Invoke(role) is { } label && !string.IsNullOrWhiteSpace(label) ? label : Css(role);

    /// <summary>Gets the field-path name of a colour role, such as <c>surfaceAlt</c>.</summary>
    /// <param name="role">The role.</param>
    /// <returns>The name.</returns>
    public static string Field(ThemeColorRole role)
    {
        var name = role.ToString();
        return char.ToLowerInvariant(name[0]) + name[1..];
    }

    /// <summary>Gets the CSS and field-path name of a typography role, such as <c>h1</c> or <c>body</c>.</summary>
    /// <param name="role">The role.</param>
    /// <returns>The name.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="role" /> is not a defined role.</exception>
    public static string Css(TypographyRole role) => role switch
    {
        TypographyRole.H1 => "h1",
        TypographyRole.H2 => "h2",
        TypographyRole.H3 => "h3",
        TypographyRole.H4 => "h4",
        TypographyRole.H5 => "h5",
        TypographyRole.H6 => "h6",
        TypographyRole.Body => "body",
        TypographyRole.Small => "small",
        TypographyRole.Link => "link",
        TypographyRole.Button => "button",
        TypographyRole.Nav => "nav",
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, "The typography role is not defined."),
    };

    /// <summary>Gets the name of a scheme, <c>light</c> or <c>dark</c>.</summary>
    /// <param name="scheme">The scheme.</param>
    /// <returns>The name.</returns>
    public static string Css(ColorScheme scheme) => scheme == ColorScheme.Dark ? "dark" : "light";
}
