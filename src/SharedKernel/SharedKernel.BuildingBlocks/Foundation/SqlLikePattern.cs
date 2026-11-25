using System.Text;

namespace PandaBear.BuildingBlocks;

/// <summary>Builds literal contains patterns for SQL LIKE and ILIKE queries with percent and underscore wildcards.</summary>
public static class SqlLikePattern
{
    /// <summary>The escape character to supply with the pattern to the provider.</summary>
    public const string EscapeCharacter = "\\";

    /// <summary>Wraps the text in percent signs and escapes each literal percent sign, underscore, and backslash.</summary>
    /// <param name="text">The literal text, in the form the caller wants to match.</param>
    /// <returns>A parameter value for the pattern. Empty text produces <c>%%</c>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
    public static string ContainsLiteral(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var pattern = new StringBuilder(text.Length + 2).Append('%');
        foreach (var character in text)
        {
            if (character is '%' or '_' or '\\')
            {
                pattern.Append('\\');
            }

            pattern.Append(character);
        }

        return pattern.Append('%').ToString();
    }
}
