namespace PandaBear.InputNormalization;

/// <summary>
/// An immutable, case-insensitive set of allowed culture names with one default culture.
/// Names are path-safe language tags such as <c>pt</c> or <c>pt-BR</c>, so a supported name can be used as a URL segment.
/// </summary>
public sealed class SupportedCultures
{
    private readonly string[] _cultures;

    /// <summary>
    /// Initializes a new instance of the <see cref="SupportedCultures" /> class.
    /// </summary>
    /// <param name="defaultCulture">The default culture. It must be one of <paramref name="cultures" />.</param>
    /// <param name="cultures">The allowed cultures, in preferred order.</param>
    /// <exception cref="ArgumentNullException">Thrown when an argument or a culture is <see langword="null" />.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown when a name is not a path-safe language tag, a name is repeated ignoring case, or the default is not allowed.
    /// </exception>
    public SupportedCultures(string defaultCulture, IEnumerable<string> cultures)
    {
        ArgumentNullException.ThrowIfNull(defaultCulture);
        ArgumentNullException.ThrowIfNull(cultures);

        var list = new List<string>();
        foreach (var culture in cultures)
        {
            ArgumentNullException.ThrowIfNull(culture);
            if (!CultureName.IsValid(culture))
            {
                throw new ArgumentException($"'{culture}' is not a valid culture name.", nameof(cultures));
            }

            if (list.Contains(culture, StringComparer.OrdinalIgnoreCase))
            {
                throw new ArgumentException($"The culture '{culture}' is listed more than once.", nameof(cultures));
            }

            list.Add(culture);
        }

        var defaultIndex = list.FindIndex(culture => string.Equals(culture, defaultCulture, StringComparison.OrdinalIgnoreCase));
        if (defaultIndex < 0)
        {
            throw new ArgumentException("The default culture must be one of the supported cultures.", nameof(defaultCulture));
        }

        _cultures = [.. list];
        Default = list[defaultIndex];
    }

    /// <summary>
    /// Gets the default culture, spelled as it was declared in the supported list.
    /// </summary>
    public string Default
    {
        get;
    }

    /// <summary>
    /// Gets the supported cultures in declaration order.
    /// </summary>
    public IReadOnlyList<string> Cultures => _cultures;

    /// <summary>
    /// Finds a supported culture by exact name, ignoring case.
    /// </summary>
    /// <param name="value">The culture name to look up.</param>
    /// <param name="culture">The supported culture, spelled as declared, when found; otherwise <see cref="string.Empty" />.</param>
    /// <returns><see langword="true" /> when the value names a supported culture; otherwise <see langword="false" />.</returns>
    public bool TryGet(string? value, out string culture)
    {
        culture = _cultures.FirstOrDefault(candidate => string.Equals(candidate, value, StringComparison.OrdinalIgnoreCase))
            ?? string.Empty;
        return culture.Length > 0;
    }

    /// <summary>
    /// Maps a requested culture to a supported culture: the exact name, then its neutral parents
    /// (<c>pt-BR</c> to <c>pt</c>), then the first supported culture of the same language (<c>pt</c> to <c>pt-BR</c>).
    /// </summary>
    /// <param name="requested">The requested culture name, for example from an Accept-Language header.</param>
    /// <returns>The supported culture, or <see langword="null" /> when none matches.</returns>
    public string? Resolve(string? requested)
    {
        if (!CultureName.IsValid(requested))
        {
            return null;
        }

        for (var candidate = requested; candidate is not null; candidate = CultureName.Parent(candidate))
        {
            if (TryGet(candidate, out var culture))
            {
                return culture;
            }
        }

        var language = CultureName.Language(requested);
        return _cultures.FirstOrDefault(culture => string.Equals(CultureName.Language(culture), language, StringComparison.OrdinalIgnoreCase));
    }
}
