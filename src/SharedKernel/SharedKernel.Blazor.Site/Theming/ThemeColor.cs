using PandaBear.BuildingBlocks;

namespace PandaBear.Blazor.Site.Theming;

/// <summary>One colour token with a value for each scheme, rendered as <c>light-dark(light, dark)</c>.</summary>
public sealed class ThemeColor : ValueObject
{
    private ThemeColor(CssColor light, CssColor dark)
    {
        Light = light;
        Dark = dark;
    }

    /// <summary>Gets the value for the light scheme.</summary>
    public CssColor Light
    {
        get;
    }

    /// <summary>Gets the value for the dark scheme.</summary>
    public CssColor Dark
    {
        get;
    }

    /// <summary>Creates a colour pair.</summary>
    /// <param name="light">The value for the light scheme.</param>
    /// <param name="dark">The value for the dark scheme.</param>
    /// <returns>The pair.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="light" /> or <paramref name="dark" /> is <see langword="null" />.</exception>
    public static ThemeColor Create(CssColor light, CssColor dark)
    {
        ArgumentNullException.ThrowIfNull(light);
        ArgumentNullException.ThrowIfNull(dark);
        return new ThemeColor(light, dark);
    }

    /// <summary>Gets the value for a scheme.</summary>
    /// <param name="scheme">The scheme.</param>
    /// <returns>The colour.</returns>
    public CssColor For(ColorScheme scheme) => scheme == ColorScheme.Dark ? Dark : Light;

    /// <inheritdoc />
    public override string ToString() => $"light-dark({Light.Value}, {Dark.Value})";

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Light;
        yield return Dark;
    }
}
