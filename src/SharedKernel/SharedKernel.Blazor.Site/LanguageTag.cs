using System.Text.RegularExpressions;

namespace PandaBear.Blazor.Site;

/// <summary>Checks the language tags that components write into <c>lang</c>.</summary>
internal static partial class LanguageTag
{
    /// <summary>The longest accepted tag.</summary>
    private const int MaxLength = 35;

    /// <summary>
    /// Refuses a tag that is not shaped like a BCP 47 language tag: subtags of ASCII letters and digits joined by hyphens, starting with a
    /// letter subtag, such as <c>pt</c>, <c>pt-BR</c>, <c>es-419</c> or <c>zh-Hant-TW</c>. <see langword="null"/> is accepted (no tag).
    /// </summary>
    /// <param name="tag">The tag.</param>
    /// <param name="parameterName">The parameter name reported on failure.</param>
    /// <exception cref="ArgumentException"><paramref name="tag"/> is not a language tag.</exception>
    public static void Require(string? tag, string parameterName)
    {
        if (tag is not null && (tag.Length > MaxLength || !Shape().IsMatch(tag)))
        {
            throw new ArgumentException("A title language must be a BCP 47 language tag such as pt-BR.", parameterName);
        }
    }

    [GeneratedRegex("^[A-Za-z]{1,8}(-[A-Za-z0-9]{1,8})*$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 250)]
    private static partial Regex Shape();
}
