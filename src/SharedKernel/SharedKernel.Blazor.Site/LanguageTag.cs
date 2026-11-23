using PandaBear.BuildingBlocks;

namespace PandaBear.Blazor.Site;

/// <summary>Checks the language tags that components write into <c>lang</c>.</summary>
internal static class LanguageTag
{
    /// <summary>
    /// Refuses a tag that <see cref="ContentCulture.Create"/> refuses, such as one that is not shaped like <c>pt</c>, <c>pt-BR</c>,
    /// <c>es-419</c> or <c>zh-Hant-TW</c>, and a tag with surrounding white space, which <see cref="ContentCulture.Create"/> would trim
    /// but <c>lang</c> would keep. <see langword="null"/> is accepted (no tag).
    /// </summary>
    /// <param name="tag">The tag.</param>
    /// <param name="parameterName">The parameter name reported on failure.</param>
    /// <exception cref="ArgumentException"><paramref name="tag"/> is not a language tag.</exception>
    public static void Require(string? tag, string parameterName)
    {
        if (tag is not null && (tag.Length != tag.AsSpan().Trim().Length || ContentCulture.Create(tag).IsFailure))
        {
            throw new ArgumentException("A title language must be a BCP 47 language tag such as pt-BR.", parameterName);
        }
    }
}
