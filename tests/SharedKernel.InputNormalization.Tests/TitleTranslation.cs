namespace PandaBear.InputNormalization.Tests;

/// <summary>A translated title used to exercise <see cref="TranslationSet{T}" />.</summary>
/// <param name="Culture">The culture name of the translation.</param>
/// <param name="Text">The translated text.</param>
internal sealed record TitleTranslation(string Culture, string Text)
{
    /// <summary>Builds a set with one translation per culture whose text is <c>title-{culture}</c>.</summary>
    public static TranslationSet<TitleTranslation> SetOf(params string[] cultures)
    {
        return new([.. cultures.Select(static c => new TitleTranslation(c, $"title-{c}"))], static t => t.Culture);
    }
}
