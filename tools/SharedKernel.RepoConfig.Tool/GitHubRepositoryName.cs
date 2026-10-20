namespace SharedKernel.RepoConfig.Tool;

internal static class GitHubRepositoryName
{
    public static bool IsValid(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var parts = value.Split('/');
        return parts.Length == 2
            && IsValidOwner(parts[0])
            && IsValidRepository(parts[1]);
    }

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
