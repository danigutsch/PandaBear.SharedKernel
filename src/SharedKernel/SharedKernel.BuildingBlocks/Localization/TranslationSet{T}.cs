using System.Diagnostics.CodeAnalysis;

namespace PandaBear.BuildingBlocks;

/// <summary>The translations of one entity, one per <see cref="ContentCulture"/>, with culture fallback. Create it with <see cref="TranslationSet.Create{T}"/>.</summary>
/// <typeparam name="T">The translation type.</typeparam>
public sealed class TranslationSet<T>
    where T : class
{
    private readonly Dictionary<ContentCulture, T> _byCulture;
    private readonly ContentCulture[] _order;

    /// <summary>Initializes the set from translations already checked for unique cultures.</summary>
    /// <param name="byCulture">The translations by culture.</param>
    /// <param name="order">The cultures of <paramref name="byCulture"/> in the order the translations were given.</param>
    internal TranslationSet(Dictionary<ContentCulture, T> byCulture, ContentCulture[] order)
    {
        _byCulture = byCulture;
        _order = order;
    }

    /// <summary>
    /// Resolves the best translation in the same order as <see cref="SupportedCultures.Resolve(ContentCulture?)"/>: the requested
    /// culture, its neutral parents (<c>pt-BR</c> to <c>pt</c>), then the first translation of the same language in the order the
    /// translations were given (<c>pt</c> to <c>pt-BR</c>), then the default culture and its neutral parents.
    /// </summary>
    /// <param name="requested">The requested culture; <see langword="null"/> skips straight to the default.</param>
    /// <param name="defaultCulture">The culture used when the requested one has no translation.</param>
    /// <param name="translation">The resolved translation when found.</param>
    /// <returns><see langword="true"/> when a translation was found; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="defaultCulture"/> is <see langword="null"/>.</exception>
    public bool TryResolve(ContentCulture? requested, ContentCulture defaultCulture, [MaybeNullWhen(false)] out T translation)
    {
        ArgumentNullException.ThrowIfNull(defaultCulture);

        return TryFind(requested, out translation)
            || TryFindSameLanguage(requested, out translation)
            || TryFind(defaultCulture, out translation);
    }

    private bool TryFind(ContentCulture? culture, [MaybeNullWhen(false)] out T translation)
    {
        for (var candidate = culture; candidate is not null; candidate = candidate.Parent)
        {
            if (_byCulture.TryGetValue(candidate, out translation))
            {
                return true;
            }
        }

        translation = null;
        return false;
    }

    private bool TryFindSameLanguage(ContentCulture? culture, [MaybeNullWhen(false)] out T translation)
    {
        if (culture is not null)
        {
            var language = culture.Language;
            foreach (var candidate in _order)
            {
                if (candidate.Language == language)
                {
                    translation = _byCulture[candidate];
                    return true;
                }
            }
        }

        translation = null;
        return false;
    }
}
