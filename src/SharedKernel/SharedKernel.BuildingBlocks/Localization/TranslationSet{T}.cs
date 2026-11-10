using System.Diagnostics.CodeAnalysis;

namespace PandaBear.BuildingBlocks;

/// <summary>The translations of one entity, one per <see cref="ContentCulture"/>, with culture fallback. Create it with <see cref="TranslationSet.Create{T}"/>.</summary>
/// <typeparam name="T">The translation type.</typeparam>
public sealed class TranslationSet<T>
    where T : class
{
    private readonly Dictionary<ContentCulture, T> _byCulture;

    /// <summary>Initializes the set from translations already checked for unique cultures.</summary>
    /// <param name="byCulture">The translations by culture.</param>
    internal TranslationSet(Dictionary<ContentCulture, T> byCulture) => _byCulture = byCulture;

    /// <summary>
    /// Resolves the best translation: the requested culture, its neutral parents (<c>pt-BR</c> to <c>pt</c>), then the default culture
    /// and its neutral parents.
    /// </summary>
    /// <param name="requested">The requested culture; <see langword="null"/> skips straight to the default.</param>
    /// <param name="defaultCulture">The culture used when the requested one has no translation.</param>
    /// <param name="translation">The resolved translation when found.</param>
    /// <returns><see langword="true"/> when a translation was found; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="defaultCulture"/> is <see langword="null"/>.</exception>
    public bool TryResolve(ContentCulture? requested, ContentCulture defaultCulture, [MaybeNullWhen(false)] out T translation)
    {
        ArgumentNullException.ThrowIfNull(defaultCulture);

        return TryFind(requested, out translation) || TryFind(defaultCulture, out translation);
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
}
