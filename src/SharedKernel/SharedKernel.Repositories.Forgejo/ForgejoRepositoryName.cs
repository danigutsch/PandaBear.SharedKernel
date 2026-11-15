namespace PandaBear.Repositories.Forgejo;

/// <summary>Checks the <c>owner/name</c> form of a Forgejo repository, which becomes part of request addresses.</summary>
internal static class ForgejoRepositoryName
{
    /// <summary>Splits <paramref name="repository"/> into its owner and name, or returns <see langword="false"/>.</summary>
    /// <remarks>
    /// Each part holds letters, digits, <c>-</c>, <c>.</c>, and <c>_</c>, and neither is <c>.</c> or <c>..</c>, so the
    /// value cannot add a path segment or a query to a request address.
    /// </remarks>
    public static bool TryParse(string repository, out string owner, out string name)
    {
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
