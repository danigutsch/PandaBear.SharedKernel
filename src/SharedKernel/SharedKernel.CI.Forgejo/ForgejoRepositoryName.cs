namespace PandaBear.CI.Forgejo;

/// <summary>Checks the <c>owner/name</c> form of a Forgejo repository, which becomes part of request addresses.</summary>
internal static class ForgejoRepositoryName
{
    /// <summary>Splits <paramref name="repository"/> into its owner and name.</summary>
    /// <exception cref="ArgumentException">
    /// The value is not <c>owner/name</c> with letters, digits, <c>-</c>, <c>.</c>, and <c>_</c> in each part, neither of
    /// which is <c>.</c> or <c>..</c>.
    /// </exception>
    public static (string Owner, string Name) Parse(string repository, string parameterName)
    {
        var parts = repository.Split('/');
        if (parts is not [{ Length: > 0 } owner, { Length: > 0 } name]
            || !parts.All(static part => part.All(static character => char.IsAsciiLetterOrDigit(character) || character is '-' or '.' or '_'))
            || owner is "." or ".."
            || name is "." or "..")
        {
            throw new ArgumentException("The repository must be owner/name.", parameterName);
        }

        return (owner, name);
    }
}
