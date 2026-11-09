namespace PandaBear.Secrets.Tool;

/// <summary>Finds the files named <c>*.sops.*</c> under a root, without following symbolic links and without entering VCS or build directories.</summary>
internal static class SopsFileFinder
{
    private static readonly HashSet<string> SkippedDirectories = new(StringComparer.Ordinal) { ".git", "node_modules", "bin", "obj" };

    /// <summary>Returns the root-relative paths with <c>/</c> separators, ordered ordinally.</summary>
    public static IReadOnlyList<string> Find(string root)
    {
        List<string> found = [];
        Walk(new DirectoryInfo(root), string.Empty, found);
        found.Sort(StringComparer.Ordinal);
        return found;
    }

    private static void Walk(DirectoryInfo directory, string prefix, List<string> found)
    {
        foreach (var entry in directory.EnumerateFileSystemInfos())
        {
            if (entry.LinkTarget is not null)
            {
                continue;
            }

            var relative = prefix + entry.Name;
            if (entry is DirectoryInfo child)
            {
                if (!SkippedDirectories.Contains(entry.Name))
                {
                    Walk(child, relative + "/", found);
                }
            }
            else if (entry.Name.Contains(".sops.", StringComparison.Ordinal))
            {
                found.Add(relative);
            }
        }
    }
}
