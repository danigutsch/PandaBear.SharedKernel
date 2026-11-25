using SharedKernel.Results;

namespace SharedKernel.BuildingBlocks;

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
        var order = new List<ContentCulture>();
        foreach (var translation in translations)
        {
            ArgumentNullException.ThrowIfNull(translation);
            var culture = cultureSelector(translation);
            if (!byCulture.TryAdd(culture, translation))
            {
                const string message = "Translations cannot contain the same culture twice.";
                return Result.Invalid<TranslationSet<T>>(detail: message, field: "translations", message: message);
            }

            order.Add(culture);
        }

        return Result.Ok(new TranslationSet<T>(byCulture, [.. order]));
    }
}
