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

    /// <summary>
    /// Splits <paramref name="repository"/> into its owner and name, as <see cref="TryParse"/> does, or fails with one
    /// <see cref="ArgumentException"/> that every Forgejo client shares.
    /// </summary>
    /// <param name="repository">The repository, such as <c>owner/name</c>.</param>
    /// <param name="paramName">The name of the caller's parameter that holds <paramref name="repository"/>, for the exception.</param>
    /// <returns>The owner and the name.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="repository"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="repository"/> is not a valid <c>owner/name</c>.</exception>
    public static (string Owner, string Name) Parse(string repository, string paramName)
    {
        ArgumentNullException.ThrowIfNull(repository, paramName);
        return TryParse(repository, out var owner, out var name)
            ? (owner, name)
            : throw new ArgumentException(
                "The repository must be owner/name, each part of ASCII letters, digits, '-', '.', and '_', and neither part '.' nor '..'.",
                paramName);
    }
}
