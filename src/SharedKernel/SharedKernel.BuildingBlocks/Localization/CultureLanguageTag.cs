namespace PandaBear.BuildingBlocks;

/// <summary>
/// The language tag that one culture declares, for <see cref="CultureLanguageTags.Create"/>: the culture <c>pt</c> (the content key and
/// the address segment) declaring <c>pt-BR</c>, the language its content is written in.
/// </summary>
public sealed record CultureLanguageTag
{
    /// <summary>Initializes the language tag of a culture.</summary>
    /// <param name="culture">The culture, one of the supported cultures, such as <c>pt</c>.</param>
    /// <param name="languageTag">The language tag the culture declares, such as <c>pt-BR</c>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="culture"/> or <paramref name="languageTag"/> is <see langword="null"/>.</exception>
    public CultureLanguageTag(ContentCulture culture, ContentCulture languageTag)
    {
        ArgumentNullException.ThrowIfNull(culture);
        ArgumentNullException.ThrowIfNull(languageTag);

        Culture = culture;
        LanguageTag = languageTag;
    }

    /// <summary>Gets the culture: the content key and the address segment, such as <c>pt</c>.</summary>
    public ContentCulture Culture { get; }

    /// <summary>Gets the language tag the culture declares, such as <c>pt-BR</c>.</summary>
    public ContentCulture LanguageTag { get; }
}
