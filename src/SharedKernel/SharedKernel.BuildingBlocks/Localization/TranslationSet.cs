using PandaBear.Results;

namespace PandaBear.BuildingBlocks;

/// <summary>Creates <see cref="TranslationSet{T}"/> values.</summary>
public static class TranslationSet
{
    /// <summary>Creates a set from translations.</summary>
    /// <typeparam name="T">The translation type.</typeparam>
    /// <param name="translations">The translations.</param>
    /// <param name="cultureSelector">Selects the culture of a translation.</param>
    /// <returns>The set or a validation error when two translations share a culture.</returns>
    /// <exception cref="ArgumentNullException">An argument or a translation is <see langword="null"/>.</exception>
    public static Result<TranslationSet<T>> Create<T>(IEnumerable<T> translations, Func<T, ContentCulture> cultureSelector)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(translations);
        ArgumentNullException.ThrowIfNull(cultureSelector);

        var byCulture = new Dictionary<ContentCulture, T>();
        foreach (var translation in translations)
        {
            ArgumentNullException.ThrowIfNull(translation);
            if (!byCulture.TryAdd(cultureSelector(translation), translation))
            {
                const string message = "Translations cannot contain the same culture twice.";
                return Result.Invalid<TranslationSet<T>>(detail: message, field: "translations", message: message);
            }
        }

        return Result.Ok(new TranslationSet<T>(byCulture));
    }
}
