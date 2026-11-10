using System.Diagnostics.CodeAnalysis;

namespace PandaBear.InputNormalization;

/// <summary>
/// An immutable set of translations of one entity, keyed by culture name, with culture fallback.
/// </summary>
/// <typeparam name="T">The translation type.</typeparam>
public sealed class TranslationSet<T>
    where T : class
{
    private readonly Dictionary<string, T> _byCulture = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Initializes a new instance of the <see cref="TranslationSet{T}" /> class.
    /// </summary>
    /// <param name="translations">The translations.</param>
    /// <param name="cultureSelector">Selects the culture name of a translation.</param>
    /// <exception cref="ArgumentNullException">Thrown when an argument or a translation is <see langword="null" />.</exception>
    /// <exception cref="ArgumentException">Thrown when a culture name is blank or two translations share a culture ignoring case.</exception>
    public TranslationSet(IEnumerable<T> translations, Func<T, string> cultureSelector)
    {
        ArgumentNullException.ThrowIfNull(translations);
        ArgumentNullException.ThrowIfNull(cultureSelector);

        foreach (var translation in translations)
        {
            ArgumentNullException.ThrowIfNull(translation);
            var culture = cultureSelector(translation);
            ArgumentException.ThrowIfNullOrWhiteSpace(culture);
            if (!_byCulture.TryAdd(culture, translation))
            {
                throw new ArgumentException($"More than one translation exists for the culture '{culture}'.", nameof(translations));
            }
        }
    }

    /// <summary>
    /// Resolves the best translation: the requested culture, its neutral parents (<c>pt-BR</c> to <c>pt</c>),
    /// then the default culture and its neutral parents.
    /// </summary>
    /// <param name="requestedCulture">The requested culture name; may be <see langword="null" /> or blank.</param>
    /// <param name="defaultCulture">The culture used when the requested one has no translation.</param>
    /// <param name="translation">The resolved translation when found.</param>
    /// <returns><see langword="true" /> when a translation was found; otherwise <see langword="false" />.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="defaultCulture" /> is <see langword="null" />.</exception>
    public bool TryResolve(string? requestedCulture, string defaultCulture, [MaybeNullWhen(false)] out T translation)
    {
        ArgumentNullException.ThrowIfNull(defaultCulture);

        return TryFind(requestedCulture, out translation) || TryFind(defaultCulture, out translation);
    }

    private bool TryFind(string? culture, [MaybeNullWhen(false)] out T translation)
    {
        for (var candidate = culture; !string.IsNullOrWhiteSpace(candidate); candidate = CultureName.Parent(candidate))
        {
            if (_byCulture.TryGetValue(candidate, out translation))
            {
                return true;
            }
        }

        translation = null;
        return false;
    }
}
