namespace PandaBear.RepoConfig.Tool;

/// <summary>
/// Validates and resolves the repository-relative paths that configuration files name.
/// </summary>
internal static class RepositoryPath
{
    /// <summary>
    /// Reports whether <paramref name="path"/> is repository-relative: segments separated by <c>/</c>, none of them
    /// empty, <c>.</c>, or <c>..</c>, without a root, a <c>\</c>, or a <c>:</c>.
    /// </summary>
    public static bool IsRelative(string path) =>
        !string.IsNullOrWhiteSpace(path)
        && !Path.IsPathRooted(path)
        && !path.Contains('\\', StringComparison.Ordinal)
        && !path.Contains(':', StringComparison.Ordinal)
        && path.Split('/').All(static segment => segment is not ("" or "." or ".."));

    /// <summary>
    /// Resolves a repository-relative path under <paramref name="root"/> without following symbolic links.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The path is not repository-relative, or the path or one of its parent directories is a symbolic link.
    /// </exception>
    public static string Resolve(string root, string relativePath)
    {
        if (!IsRelative(relativePath))
        {
            throw new InvalidOperationException("Expected a repository-relative path with '/' separators and without empty, '.', or '..' segments.");
        }

        var current = root;
        foreach (var segment in relativePath.Split('/'))
        {
            current = Path.Combine(current, segment);
            if (new FileInfo(current).LinkTarget is not null || new DirectoryInfo(current).LinkTarget is not null)
            {
                throw new InvalidOperationException("Symbolic links are not followed in repository paths.");
            }
        }

        return current;
    }
}
