using SharedKernel.BuildingBlocks;

namespace SharedKernel.Blazor.Site.Theming;

/// <summary>The typography of every <see cref="TypographyRole" />; a role that is not given is <see cref="TypographyStyle.Inherit" />.</summary>
public sealed class SiteTypography : ValueObject
{
    private static readonly TypographyRole[] Roles = Enum.GetValues<TypographyRole>();

    private readonly TypographyStyle[] styles;

    private SiteTypography(TypographyStyle[] styles) => this.styles = styles;

    /// <summary>Gets the typography that sets nothing, so every role uses the default theme.</summary>
    public static SiteTypography Empty { get; } = new(Roles.Select(_ => TypographyStyle.Inherit).ToArray());

    /// <summary>Gets the style of a role.</summary>
    /// <param name="role">The role.</param>
    /// <returns>The style.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="role" /> is not a defined role.</exception>
    public TypographyStyle this[TypographyRole role] => styles[Index(role)];

    /// <summary>Creates the typography from styles by role.</summary>
    /// <param name="styles">The styles; missing roles inherit.</param>
    /// <returns>The typography.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="styles" /> or one of its styles is <see langword="null" />.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A key is not a defined role.</exception>
    public static SiteTypography Create(IReadOnlyDictionary<TypographyRole, TypographyStyle> styles)
    {
        ArgumentNullException.ThrowIfNull(styles);
        var result = Roles.Select(_ => TypographyStyle.Inherit).ToArray();
        foreach (var (role, style) in styles)
        {
            ArgumentNullException.ThrowIfNull(style, nameof(styles));
            result[Index(role)] = style;
        }

        return new SiteTypography(result);
    }

    /// <summary>Returns a copy with the style of one role replaced.</summary>
    /// <param name="role">The role.</param>
    /// <param name="style">The style.</param>
    /// <returns>The typography.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="style" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="role" /> is not a defined role.</exception>
    public SiteTypography With(TypographyRole role, TypographyStyle style)
    {
        ArgumentNullException.ThrowIfNull(style);
        var copy = (TypographyStyle[])styles.Clone();
        copy[Index(role)] = style;
        return new SiteTypography(copy);
    }

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents() => styles;

    private static int Index(TypographyRole role) => Enum.IsDefined(role)
        ? (int)role
        : throw new ArgumentOutOfRangeException(nameof(role), role, "The typography role is not defined.");
}
