using SharedKernel.Results;

namespace SharedKernel.BuildingBlocks;

/// <summary>
/// The rules of a list of per-culture translations over <see cref="SupportedCultures"/>: each translation uses one of the cultures, each
/// culture at most once, and the default culture is required. The errors are on one field and never echo the input.
/// </summary>
public sealed class TranslationListRules
{
    private readonly string _subject;

    /// <summary>Creates the rules.</summary>
    /// <param name="cultures">The cultures a translation may use.</param>
    /// <param name="field">The field path of the errors about the list, such as <c>translations</c>.</param>
    /// <param name="subject">The word before <c>languages</c> in the message about an unsupported culture, such as <c>album</c> in
    /// <c>Each translation must use one of the album languages: pt-BR, en.</c></param>
    /// <exception cref="ArgumentNullException"><paramref name="cultures"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="field"/> or <paramref name="subject"/> is <see langword="null"/>, empty or blank.</exception>
    public TranslationListRules(SupportedCultures cultures, string field, string subject)
    {
        ArgumentNullException.ThrowIfNull(cultures);
        ArgumentException.ThrowIfNullOrWhiteSpace(field);
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        Cultures = cultures;
        Field = field;
        _subject = subject;
    }

    /// <summary>Gets the cultures a translation may use.</summary>
    public SupportedCultures Cultures { get; }

    /// <summary>Gets the field path of the errors about the list.</summary>
    public string Field { get; }

    /// <summary>
    /// Validates entered translations in order. A translation whose culture is not supported records
    /// <c>Each translation must use one of the {subject} languages: {cultures}.</c>, a second one of a culture records
    /// <c>Each language can have only one translation.</c>, and neither is built. A missing default culture records
    /// <c>A translation in the default language ({default}) is required.</c> after the errors of the translations.
    /// </summary>
    /// <typeparam name="TDefinition">The entered translation type.</typeparam>
    /// <typeparam name="T">The validated translation type.</typeparam>
    /// <param name="errors">Collects the errors.</param>
    /// <param name="definitions">The entered translations; <see langword="null"/> is none.</param>
    /// <param name="cultureOf">Reads the culture name of an entered translation.</param>
    /// <param name="build">Validates an entered translation of an accepted culture and records its errors; <see langword="null"/> leaves it out.</param>
    /// <param name="parameterName">The parameter named when an entered translation is <see langword="null"/>.</param>
    /// <returns>The validated translations in entered order.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="errors"/>, <paramref name="cultureOf"/> or <paramref name="build"/> is
    /// <see langword="null"/>, or an entered translation is (named <paramref name="parameterName"/>).</exception>
    public IReadOnlyList<T> Validate<TDefinition, T>(
        ValidationErrors errors,
        IEnumerable<TDefinition?>? definitions,
        Func<TDefinition, string?> cultureOf,
        Func<ContentCulture, TDefinition, T?> build,
        string parameterName)
        where TDefinition : class
        where T : class
    {
        ArgumentNullException.ThrowIfNull(errors);
        ArgumentNullException.ThrowIfNull(cultureOf);
        ArgumentNullException.ThrowIfNull(build);

        var seen = new HashSet<ContentCulture>();
        var translations = new List<T>();
        foreach (var definition in definitions ?? [])
        {
            ArgumentNullException.ThrowIfNull(definition, parameterName);
            if (Culture(errors, cultureOf(definition), seen) is { } culture && build(culture, definition) is { } translation)
            {
                translations.Add(translation);
            }
        }

        if (!seen.Contains(Cultures.Default))
        {
            Add(errors, $"A translation in the default language ({Cultures.Default}) is required.");
        }

        return translations.AsReadOnly();
    }

    /// <summary>
    /// Replaces the current translations with new ones in place: the instance of a culture that stays is kept and updated (so a persistence
    /// layer updates its row instead of deleting and inserting one), a new culture is added and a culture that is gone is removed.
    /// </summary>
    /// <typeparam name="T">The translation type.</typeparam>
    /// <param name="current">The current translations; changed in place.</param>
    /// <param name="replacement">The validated new translations.</param>
    /// <param name="culture">Reads a translation's culture.</param>
    /// <param name="apply">Copies the new translation (second) into the kept one (first).</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static void Merge<T>(ICollection<T> current, IReadOnlyList<T> replacement, Func<T, ContentCulture> culture, Action<T, T> apply)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(replacement);
        ArgumentNullException.ThrowIfNull(culture);
        ArgumentNullException.ThrowIfNull(apply);

        foreach (var gone in current.Where(existing => !replacement.Any(item => culture(item) == culture(existing))).ToList())
        {
            current.Remove(gone);
        }

        foreach (var item in replacement)
        {
            var existing = current.FirstOrDefault(candidate => culture(candidate) == culture(item));
            if (existing is null)
            {
                current.Add(item);
            }
            else
            {
                apply(existing, item);
            }
        }
    }

    /// <summary>Whether the replacement translations equal the current ones language by language, in any order, so replacing them changes nothing.</summary>
    /// <typeparam name="T">The translation type.</typeparam>
    /// <param name="current">The current translations.</param>
    /// <param name="replacement">The validated replacement.</param>
    /// <param name="culture">Reads a translation's culture.</param>
    /// <param name="same">Compares two translations of one culture.</param>
    /// <returns><see langword="true"/> when nothing would change.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static bool Same<T>(IReadOnlyList<T> current, IReadOnlyList<T> replacement, Func<T, ContentCulture> culture, Func<T, T, bool> same)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(replacement);
        ArgumentNullException.ThrowIfNull(culture);
        ArgumentNullException.ThrowIfNull(same);

        return current.Count == replacement.Count
            && replacement.All(item => current.Any(existing => culture(existing) == culture(item) && same(existing, item)));
    }

    private ContentCulture? Culture(ValidationErrors errors, string? value, HashSet<ContentCulture> seen)
    {
        if (!Cultures.TryGet(value, out var culture))
        {
            Add(errors, $"Each translation must use one of the {_subject} languages: {Cultures}.");
            return null;
        }

        if (!seen.Add(culture))
        {
            Add(errors, "Each language can have only one translation.");
            return null;
        }

        return culture;
    }

    private void Add(ValidationErrors errors, string message) => errors.Add(Result.Invalid(detail: message, field: Field, message: message));
}
