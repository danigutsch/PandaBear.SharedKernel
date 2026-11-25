using SharedKernel.Domain;

namespace SharedKernel.Blazor.Site.Theming;

/// <summary>
/// The colour tokens of a site theme, each with a light and a dark value. The two hover colours are optional: when one is not
/// given it is derived from its base colour mixed with <see cref="DerivedHoverWeight" /> of the text colour, per scheme, so a hover
/// darkens in the light scheme and lightens in the dark one (with a dark text colour on a light surface and the other way round).
/// </summary>
public sealed class SiteColors : ValueObject
{
    /// <summary>The share of the text colour mixed into a base colour to derive a hover colour that was not given.</summary>
    public const double DerivedHoverWeight = 0.2;

    private readonly ThemeColor?[] given;
    private readonly ThemeColor[] resolved;

    private SiteColors(ThemeColor?[] given)
    {
        this.given = given;
        resolved = new ThemeColor[given.Length];
        for (var index = 0; index < given.Length; index++)
        {
            resolved[index] = given[index]!;
        }

        resolved[(int)ThemeColorRole.PrimaryHover] = given[(int)ThemeColorRole.PrimaryHover] ?? Derive(ThemeColorRole.Primary);
        resolved[(int)ThemeColorRole.AccentHover] = given[(int)ThemeColorRole.AccentHover] ?? Derive(ThemeColorRole.Accent);
    }

    /// <summary>Gets the primary colour.</summary>
    public ThemeColor Primary => this[ThemeColorRole.Primary];

    /// <summary>Gets the primary hover colour, given or derived.</summary>
    public ThemeColor PrimaryHover => this[ThemeColorRole.PrimaryHover];

    /// <summary>Gets the colour of text on the primary colour.</summary>
    public ThemeColor OnPrimary => this[ThemeColorRole.OnPrimary];

    /// <summary>Gets the accent colour.</summary>
    public ThemeColor Accent => this[ThemeColorRole.Accent];

    /// <summary>Gets the accent hover colour, given or derived.</summary>
    public ThemeColor AccentHover => this[ThemeColorRole.AccentHover];

    /// <summary>Gets the colour of text on the accent colour.</summary>
    public ThemeColor OnAccent => this[ThemeColorRole.OnAccent];

    /// <summary>Gets the text colour.</summary>
    public ThemeColor Text => this[ThemeColorRole.Text];

    /// <summary>Gets the secondary text colour.</summary>
    public ThemeColor Muted => this[ThemeColorRole.Muted];

    /// <summary>Gets the page background colour.</summary>
    public ThemeColor Surface => this[ThemeColorRole.Surface];

    /// <summary>Gets the alternative background of cards and quotes.</summary>
    public ThemeColor SurfaceAlt => this[ThemeColorRole.SurfaceAlt];

    /// <summary>Gets the border colour.</summary>
    public ThemeColor Border => this[ThemeColorRole.Border];

    /// <summary>Gets the focus ring colour.</summary>
    public ThemeColor Focus => this[ThemeColorRole.Focus];

    /// <summary>Gets the colour of a role; a hover colour that was not given is the derived one.</summary>
    /// <param name="role">The role.</param>
    /// <returns>The colour.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="role" /> is not a defined role.</exception>
    public ThemeColor this[ThemeColorRole role] => Enum.IsDefined(role)
        ? resolved[(int)role]
        : throw new ArgumentOutOfRangeException(nameof(role), role, "The colour role is not defined.");

    /// <summary>Creates the colour tokens.</summary>
    /// <param name="primary">Links, primary buttons and current items.</param>
    /// <param name="onPrimary">Text on the primary colour.</param>
    /// <param name="accent">Dividers and secondary buttons.</param>
    /// <param name="onAccent">Text on the accent colour.</param>
    /// <param name="text">Body text and headings.</param>
    /// <param name="muted">Secondary text.</param>
    /// <param name="surface">The page background.</param>
    /// <param name="surfaceAlt">Cards, quotes and the contact card.</param>
    /// <param name="border">Borders and rules.</param>
    /// <param name="focus">The focus ring.</param>
    /// <param name="primaryHover">The primary hover colour; derived when <see langword="null" />.</param>
    /// <param name="accentHover">The accent hover colour; derived when <see langword="null" />.</param>
    /// <returns>The colours.</returns>
    /// <exception cref="ArgumentNullException">A required colour is <see langword="null" />.</exception>
    public static SiteColors Create(
        ThemeColor primary,
        ThemeColor onPrimary,
        ThemeColor accent,
        ThemeColor onAccent,
        ThemeColor text,
        ThemeColor muted,
        ThemeColor surface,
        ThemeColor surfaceAlt,
        ThemeColor border,
        ThemeColor focus,
        ThemeColor? primaryHover = null,
        ThemeColor? accentHover = null)
    {
        ArgumentNullException.ThrowIfNull(primary);
        ArgumentNullException.ThrowIfNull(onPrimary);
        ArgumentNullException.ThrowIfNull(accent);
        ArgumentNullException.ThrowIfNull(onAccent);
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(muted);
        ArgumentNullException.ThrowIfNull(surface);
        ArgumentNullException.ThrowIfNull(surfaceAlt);
        ArgumentNullException.ThrowIfNull(border);
        ArgumentNullException.ThrowIfNull(focus);
        return new SiteColors([primary, primaryHover, onPrimary, accent, accentHover, onAccent, text, muted, surface, surfaceAlt, border, focus]);
    }

    /// <summary>Gets a value indicating whether the colour of a role was derived instead of given.</summary>
    /// <param name="role">The role.</param>
    /// <returns><see langword="true" /> for a hover colour that was not given.</returns>
    public bool IsDerived(ThemeColorRole role) => Enum.IsDefined(role) && given[(int)role] is null;

    /// <summary>Returns a copy with one colour replaced; a derived hover colour follows a new base or text colour.</summary>
    /// <param name="role">The role.</param>
    /// <param name="color">The new colour.</param>
    /// <returns>The colours.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="color" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="role" /> is not a defined role.</exception>
    public SiteColors With(ThemeColorRole role, ThemeColor color)
    {
        ArgumentNullException.ThrowIfNull(color);
        if (!Enum.IsDefined(role))
        {
            throw new ArgumentOutOfRangeException(nameof(role), role, "The colour role is not defined.");
        }

        var copy = (ThemeColor?[])given.Clone();
        copy[(int)role] = color;
        return new SiteColors(copy);
    }

    /// <summary>Returns a copy whose hover colour of a role is derived again.</summary>
    /// <param name="role"><see cref="ThemeColorRole.PrimaryHover" /> or <see cref="ThemeColorRole.AccentHover" />.</param>
    /// <returns>The colours.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="role" /> is not a hover role.</exception>
    public SiteColors WithDerived(ThemeColorRole role)
    {
        if (role is not (ThemeColorRole.PrimaryHover or ThemeColorRole.AccentHover))
        {
            throw new ArgumentOutOfRangeException(nameof(role), role, "Only a hover colour can be derived.");
        }

        var copy = (ThemeColor?[])given.Clone();
        copy[(int)role] = null;
        return new SiteColors(copy);
    }

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        foreach (var color in given)
        {
            yield return color;
        }
    }

    private ThemeColor Derive(ThemeColorRole baseRole)
    {
        var baseColor = resolved[(int)baseRole];
        var text = resolved[(int)ThemeColorRole.Text];
        return ThemeColor.Create(
            baseColor.Light.Mix(text.Light, DerivedHoverWeight),
            baseColor.Dark.Mix(text.Dark, DerivedHoverWeight));
    }
}
