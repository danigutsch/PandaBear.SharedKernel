namespace PandaBear.RepoConfig.Tool;

/// <summary>Validates GitHub repository names.</summary>
internal static class GitHubRepositoryName
{
    /// <summary>Returns whether the value is a valid <c>owner/name</c> repository name.</summary>
    /// <param name="value">Value to check.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
    public static bool IsValid(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var parts = value.Split('/');
        return parts.Length == 2
            && IsValidOwner(parts[0])
            && IsValidRepository(parts[1]);
    }

    /// <summary>Returns whether the value is a valid GitHub owner login.</summary>
    /// <param name="value">Value to check.</param>
    internal static bool IsValidOwner(string value) => value.Length is > 0 and <= 39
        && value[0] != '-'
        && value[^1] != '-'
        && !value.Contains("--", StringComparison.Ordinal)
        && value.All(character => char.IsAsciiLetterOrDigit(character) || character == '-');

    private static bool IsValidRepository(string value) => value.Length is > 0 and <= 100
        && !string.Equals(value, ".", StringComparison.Ordinal)
        && !string.Equals(value, "..", StringComparison.Ordinal)
        && value.All(character => char.IsAsciiLetterOrDigit(character)
            || character is '-' or '_' or '.');
}
