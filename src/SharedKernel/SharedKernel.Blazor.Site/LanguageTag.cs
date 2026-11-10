namespace PandaBear.Blazor.Site;

/// <summary>Checks that a language tag is safe to write into <c>lang</c> and <c>hreflang</c> attributes.</summary>
internal static class LanguageTag
{
    /// <summary>Requires a tag of dash-separated alphanumeric subtags whose first subtag is a language such as <c>en</c> or <c>pt</c>.</summary>
    /// <param name="culture">The tag.</param>
    /// <param name="parameterName">The parameter name reported on failure.</param>
    /// <returns>The unchanged tag.</returns>
    /// <exception cref="ArgumentException">The tag is blank or malformed.</exception>
    public static string Require(string? culture, string parameterName)
    {
        var subtags = (culture ?? string.Empty).Split('-');
        if (!subtags.All(IsSubtag) || subtags[0].Length < 2 || !subtags[0].All(char.IsAsciiLetter))
        {
            throw new ArgumentException("A language tag such as 'en' or 'pt-BR' is required.", parameterName);
        }

        return culture!;
    }

    private static bool IsSubtag(string subtag) => subtag.Length is >= 1 and <= 8 && subtag.All(char.IsAsciiLetterOrDigit);
}
