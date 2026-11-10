namespace PandaBear.InputNormalization;

/// <summary>
/// Internal helpers for culture names such as <c>pt</c> or <c>pt-BR</c>.
/// </summary>
internal static class CultureName
{
    private const int MaxLength = 35;

    /// <summary>
    /// Determines whether a name is a path-safe language tag: hyphen-separated, non-empty ASCII letter or digit parts.
    /// </summary>
    public static bool IsValid([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] string? name)
    {
        if (string.IsNullOrEmpty(name) || name.Length > MaxLength)
        {
            return false;
        }

        var partLength = 0;
        foreach (var character in name)
        {
            if (char.IsAsciiLetterOrDigit(character))
            {
                partLength++;
            }
            else if (character == '-' && partLength > 0)
            {
                partLength = 0;
            }
            else
            {
                return false;
            }
        }

        return partLength > 0;
    }

    /// <summary>
    /// Returns the parent of a culture name (<c>pt-BR</c> gives <c>pt</c>), or <see langword="null" /> for a neutral name.
    /// </summary>
    public static string? Parent(string name)
    {
        var index = name.LastIndexOf('-');
        return index > 0 ? name[..index] : null;
    }

    /// <summary>
    /// Returns the language part of a culture name (<c>pt-BR</c> gives <c>pt</c>).
    /// </summary>
    public static string Language(string name)
    {
        var index = name.IndexOf('-', StringComparison.Ordinal);
        return index > 0 ? name[..index] : name;
    }
}
