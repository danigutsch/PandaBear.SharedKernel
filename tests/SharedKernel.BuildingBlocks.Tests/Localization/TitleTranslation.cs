namespace PandaBear.BuildingBlocks.Tests;

/// <summary>A translated title used to exercise <see cref="TranslationSet{T}" />.</summary>
/// <param name="Culture">The culture of the translation.</param>
/// <param name="Text">The translated text.</param>
internal sealed record TitleTranslation(ContentCulture Culture, string Text)
{
    /// <summary>Builds a translation for the culture whose text is <c>title-{culture}</c>.</summary>
    public static TitleTranslation For(string culture)
    {
        var parsed = ContentCulture.Restore(culture);
        return new TitleTranslation(parsed, $"title-{parsed.Value}");
    }
}
