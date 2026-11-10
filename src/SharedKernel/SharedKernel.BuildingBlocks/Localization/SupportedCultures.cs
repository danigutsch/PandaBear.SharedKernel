using System.Diagnostics.CodeAnalysis;
using PandaBear.Results;

namespace PandaBear.BuildingBlocks;

/// <summary>An immutable set of allowed <see cref="ContentCulture"/> values with one default culture, for example the languages of a site.</summary>
public sealed class SupportedCultures : ValueObject
{
    private readonly ContentCulture[] _cultures;

    private SupportedCultures(ContentCulture defaultCulture, ContentCulture[] cultures)
    {
        Default = defaultCulture;
        _cultures = cultures;
    }

    /// <summary>Gets the default culture. It is one of <see cref="Cultures"/>.</summary>
    public ContentCulture Default
    {
        get;
    }

    /// <summary>Gets the allowed cultures in declaration order.</summary>
    public IReadOnlyList<ContentCulture> Cultures => _cultures;

    /// <summary>Creates a set of allowed cultures.</summary>
    /// <param name="defaultCulture">The default culture; it must be one of <paramref name="cultures"/>.</param>
    /// <param name="cultures">The allowed cultures in preferred order; at least one, without repeats.</param>
    /// <returns>The set or a validation error.</returns>
    /// <exception cref="ArgumentNullException">An argument or a culture is <see langword="null"/>.</exception>
    public static Result<SupportedCultures> Create(ContentCulture defaultCulture, IEnumerable<ContentCulture> cultures)
    {
        ArgumentNullException.ThrowIfNull(defaultCulture);
        ArgumentNullException.ThrowIfNull(cultures);

        var list = new List<ContentCulture>();
        foreach (var culture in cultures)
        {
            ArgumentNullException.ThrowIfNull(culture);
            if (list.Contains(culture))
            {
                return Invalid("Cultures cannot contain the same culture twice.");
            }

            list.Add(culture);
        }

        if (list.Count == 0)
        {
            return Invalid("At least one culture is required.");
        }

        return list.Contains(defaultCulture)
            ? Result.Ok(new SupportedCultures(defaultCulture, [.. list]))
            : Invalid("The default culture must be one of the supported cultures.");
    }

    /// <summary>Determines whether a culture is one of the allowed cultures (exact match).</summary>
    /// <param name="culture">The culture.</param>
    /// <returns><see langword="true"/> when it is allowed; otherwise, <see langword="false"/>.</returns>
    public bool Contains(ContentCulture culture) => _cultures.Contains(culture);

    /// <summary>Finds an allowed culture by exact name, ignoring case.</summary>
    /// <param name="value">The culture name, for example a URL segment.</param>
    /// <param name="culture">The allowed culture when found.</param>
    /// <returns><see langword="true"/> when the text names an allowed culture; otherwise, <see langword="false"/>.</returns>
    public bool TryGet(string? value, [NotNullWhen(true)] out ContentCulture? culture)
    {
        var parsed = ContentCulture.Create(value);
        culture = parsed.IsSuccess && Contains(parsed.Value) ? parsed.Value : null;
        return culture is not null;
    }

    /// <summary>Maps a requested culture to an allowed culture: the exact culture, then its neutral parents (<c>pt-BR</c> to <c>pt</c>), then the first allowed culture of the same language (<c>pt</c> to <c>pt-BR</c>).</summary>
    /// <param name="requested">The requested culture, for example from an Accept-Language header.</param>
    /// <returns>The allowed culture, or <see langword="null"/> when none matches.</returns>
    public ContentCulture? Resolve(ContentCulture? requested)
    {
        for (var candidate = requested; candidate is not null; candidate = candidate.Parent)
        {
            if (Contains(candidate))
            {
                return candidate;
            }
        }

        return requested is null ? null : _cultures.FirstOrDefault(culture => culture.Language == requested.Language);
    }

    /// <summary>Maps requested culture text to an allowed culture; see <see cref="Resolve(ContentCulture?)"/>.</summary>
    /// <param name="requested">The requested culture name; text that is not a culture name matches nothing.</param>
    /// <returns>The allowed culture, or <see langword="null"/> when none matches.</returns>
    public ContentCulture? Resolve(string? requested)
    {
        var parsed = ContentCulture.Create(requested);
        return parsed.IsSuccess ? Resolve(parsed.Value) : null;
    }

    /// <inheritdoc />
    public override string ToString() => string.Join(", ", _cultures.Select(static culture => culture.Value));

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Default;
        foreach (var culture in _cultures)
        {
            yield return culture;
        }
    }

    private static Result<SupportedCultures> Invalid(string message) =>
        Result.Invalid<SupportedCultures>(detail: message, field: "cultures", message: message);
}
