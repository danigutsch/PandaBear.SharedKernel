using System.Diagnostics.CodeAnalysis;
using SharedKernel.Domain;
using SharedKernel.Results;

namespace SharedKernel.BuildingBlocks;

/// <summary>
/// The language tag that each culture of a <see cref="SupportedCultures"/> set declares, for a site whose culture (the content key and
/// the address segment, such as <c>pt</c>) is not the language its content is written in (<c>pt-BR</c>). A culture without a language
/// tag of its own declares itself. Pages write the declared tag to <c>lang</c>, <c>hreflang</c>, <c>Content-Language</c> and
/// <c>og:locale</c> and format numbers and dates with it, while addresses and stored keys keep the culture;
/// <see cref="TryGetCulture(ContentCulture?, out ContentCulture?)"/> maps a tag back to its culture. Every culture declares a different tag.
/// </summary>
public sealed class CultureLanguageTags : ValueObject
{
    private readonly ContentCulture[] _languageTags;

    private CultureLanguageTags(SupportedCultures cultures, ContentCulture[] languageTags)
    {
        Cultures = cultures;
        _languageTags = languageTags;
    }

    /// <summary>Gets the supported cultures whose language tags these are.</summary>
    public SupportedCultures Cultures { get; }

    /// <summary>Gets the language tag of every culture, in the order of <see cref="SupportedCultures.Cultures"/>.</summary>
    public IReadOnlyList<ContentCulture> LanguageTags => _languageTags;

    /// <summary>Gets the language tag of the default culture.</summary>
    public ContentCulture DefaultLanguageTag => LanguageTagOf(Cultures.Default);

    /// <summary>
    /// Creates the language tags of a set of cultures. Use it at start-up: a culture that is not supported, a culture given two tags,
    /// or a tag that two cultures would declare (a culture without a mapping declares itself) is invalid, and the message names it.
    /// </summary>
    /// <param name="cultures">The supported cultures.</param>
    /// <param name="languageTags">The cultures that declare another language tag than themselves, such as <c>pt</c> declaring <c>pt-BR</c>.</param>
    /// <returns>The language tags, or a validation error on the <c>languageTags</c> field.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="cultures"/>, <paramref name="languageTags"/> or one of its items is <see langword="null"/>.</exception>
    public static Result<CultureLanguageTags> Create(SupportedCultures cultures, IEnumerable<CultureLanguageTag> languageTags)
    {
        ArgumentNullException.ThrowIfNull(cultures);
        ArgumentNullException.ThrowIfNull(languageTags);

        ContentCulture[] declared = [.. cultures.Cultures];
        var mapped = new HashSet<ContentCulture>();
        foreach (var languageTag in languageTags)
        {
            ArgumentNullException.ThrowIfNull(languageTag);

            var index = IndexOf(cultures.Cultures, languageTag.Culture);
            if (index < 0)
            {
                return Invalid($"The culture '{languageTag.Culture}' is not one of the supported cultures.");
            }

            if (!mapped.Add(languageTag.Culture))
            {
                return Invalid($"The culture '{languageTag.Culture}' has more than one language tag.");
            }

            declared[index] = languageTag.LanguageTag;
        }

        var seen = new HashSet<ContentCulture>();
        foreach (var languageTag in declared)
        {
            if (!seen.Add(languageTag))
            {
                return Invalid($"The language tag '{languageTag}' belongs to more than one culture.");
            }
        }

        return Result.Ok(new CultureLanguageTags(cultures, declared));
    }

    /// <summary>Gets the language tag a culture declares: its mapped tag, or the culture itself.</summary>
    /// <param name="culture">One of the supported cultures.</param>
    /// <returns>The language tag.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="culture"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="culture"/> is not one of the supported cultures.</exception>
    public ContentCulture LanguageTagOf(ContentCulture culture)
    {
        ArgumentNullException.ThrowIfNull(culture);

        var index = IndexOf(Cultures.Cultures, culture);
        return index >= 0
            ? _languageTags[index]
            : throw new ArgumentException("The culture is not one of the supported cultures.", nameof(culture));
    }

    /// <summary>Finds the culture that declares a language tag (exact match), for example to build an address from the request culture.</summary>
    /// <param name="languageTag">The language tag, such as <c>pt-BR</c>.</param>
    /// <param name="culture">The culture that declares it when found.</param>
    /// <returns><see langword="true"/> when a culture declares the tag; otherwise, <see langword="false"/>.</returns>
    public bool TryGetCulture(ContentCulture? languageTag, [NotNullWhen(true)] out ContentCulture? culture)
    {
        var index = languageTag is null ? -1 : Array.IndexOf(_languageTags, languageTag);
        culture = index >= 0 ? Cultures.Cultures[index] : null;
        return culture is not null;
    }

    /// <summary>Finds the culture that declares a language tag given as text, ignoring case; see <see cref="TryGetCulture(ContentCulture?, out ContentCulture?)"/>.</summary>
    /// <param name="languageTag">The language tag, such as <c>CultureInfo.CurrentUICulture.Name</c>; text that is not a culture name matches nothing.</param>
    /// <param name="culture">The culture that declares it when found.</param>
    /// <returns><see langword="true"/> when a culture declares the tag; otherwise, <see langword="false"/>.</returns>
    public bool TryGetCulture(string? languageTag, [NotNullWhen(true)] out ContentCulture? culture)
    {
        var parsed = ContentCulture.Create(languageTag);
        culture = null;
        return parsed.IsSuccess && TryGetCulture(parsed.Value, out culture);
    }

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Cultures;
        foreach (var languageTag in _languageTags)
        {
            yield return languageTag;
        }
    }

    private static int IndexOf(IReadOnlyList<ContentCulture> cultures, ContentCulture culture)
    {
        for (var index = 0; index < cultures.Count; index++)
        {
            if (cultures[index] == culture)
            {
                return index;
            }
        }

        return -1;
    }

    private static Result<CultureLanguageTags> Invalid(string message) =>
        Result.Invalid<CultureLanguageTags>(detail: message, field: "languageTags", message: message);
}
