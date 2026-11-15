namespace PandaBear.Forgejo;

/// <summary>Checks the <c>owner/name</c> form of a Forgejo repository before it becomes part of a request address.</summary>
public static class ForgejoRepositoryName
{
    /// <summary>
    /// Splits <paramref name="repository"/> into its owner and name when it is <c>owner/name</c> with ASCII letters,
    /// digits, <c>-</c>, <c>.</c>, and <c>_</c> in each part, neither of which is <c>.</c> or <c>..</c>, so the value can
    /// add neither a path segment nor a query to a request address.
    /// </summary>
    /// <param name="repository">The repository, such as <c>owner/name</c>.</param>
    /// <param name="owner">The owner, or an empty string when the value is not valid.</param>
    /// <param name="name">The name, or an empty string when the value is not valid.</param>
    /// <returns><see langword="true"/> when the value is a valid <c>owner/name</c>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="repository"/> is <see langword="null"/>.</exception>
    public static bool TryParse(string repository, out string owner, out string name)
    {
        ArgumentNullException.ThrowIfNull(repository);
        var parts = repository.Split('/');
        if (parts is [{ Length: > 0 } first, { Length: > 0 } second]
            && parts.All(static part => part.All(static character => char.IsAsciiLetterOrDigit(character) || character is '-' or '.' or '_'))
            && first is not ("." or "..")
            && second is not ("." or ".."))
        {
            owner = first;
            name = second;
            return true;
        }

        owner = string.Empty;
        name = string.Empty;
        return false;
    }
}
