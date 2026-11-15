namespace PandaBear.CI.GitHub;

/// <summary>Checks the <c>owner/name</c> form of a GitHub repository, which becomes part of request addresses.</summary>
internal static class GitHubRepositoryName
{
    /// <summary>Splits <paramref name="repository"/> into its owner and name.</summary>
    /// <exception cref="ArgumentException">
    /// The value is not <c>owner/name</c> with letters, digits, and <c>-</c> in the owner, also <c>.</c> and <c>_</c> in the
    /// name, which is not <c>.</c> or <c>..</c>.
    /// </exception>
    public static (string Owner, string Name) Parse(string repository, string parameterName)
    {
        var parts = repository.Split('/');
        if (parts is not [{ Length: > 0 } owner, { Length: > 0 } name]
            || !owner.All(static character => char.IsAsciiLetterOrDigit(character) || character == '-')
            || !name.All(static character => char.IsAsciiLetterOrDigit(character) || character is '-' or '.' or '_')
            || name is "." or "..")
        {
            throw new ArgumentException("The repository must be owner/name.", parameterName);
        }

        return (owner, name);
    }
}
