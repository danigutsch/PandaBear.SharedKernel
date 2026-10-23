namespace PandaBear.VaultManager.Tool.Model;

/// <summary>Folder paths of a Bitwarden individual vault, where <c>/</c> separates folder levels.</summary>
internal static class FolderTree
{
    /// <summary>
    /// Returns the paths with the parent path of each. Bitwarden nests <c>A/B</c> under <c>A</c> only when a folder named
    /// exactly <c>A</c> exists, and neither its importer nor this tool's readers create missing parents otherwise.
    /// </summary>
    /// <param name="paths">Folder paths; <see langword="null"/>, empty, and whitespace paths are skipped.</param>
    /// <returns>Distinct paths in ordinal order. A parent is skipped when it is empty or ends with <c>/</c>.</returns>
    public static IReadOnlyList<string> WithParents(IEnumerable<string?> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);

        var result = new SortedSet<string>(StringComparer.Ordinal);
        foreach (string? path in paths)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                continue;
            }

            result.Add(path);
            for (int index = path.IndexOf('/', StringComparison.Ordinal); index >= 0; index = path.IndexOf('/', index + 1))
            {
                if (index > 0 && path[index - 1] != '/')
                {
                    result.Add(path[..index]);
                }
            }
        }

        return [.. result];
    }
}
