namespace PandaBear.Secrets.Tool;

/// <summary>
/// Walks a repository and returns every file, every symbolic link, and every directory it cannot list. It skips only <c>.git</c> and never follows a
/// link. Nothing is skipped by name, so a directory called <c>bin</c>, <c>obj</c>, or <c>node_modules</c> inside the scope of a rule is still checked.
/// </summary>
internal static class SopsFileFinder
{
    private const string GitDirectory = ".git";

    /// <summary>Returns the entries ordered by their relative path.</summary>
    public static IReadOnlyList<ScanEntry> Find(string root)
    {
        List<ScanEntry> found = [];
        Walk(new DirectoryInfo(root), string.Empty, found);
        found.Sort(static (left, right) => StringComparer.Ordinal.Compare(left.RelativePath, right.RelativePath));
        return found;
    }

    private static void Walk(DirectoryInfo directory, string prefix, List<ScanEntry> found)
    {
        List<FileSystemInfo> children;
        try
        {
            children = [.. directory.EnumerateFileSystemInfos()];
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            found.Add(new ScanEntry(prefix.TrimEnd('/'), directory.FullName, ScanEntryKind.UnreadableDirectory, "the directory cannot be listed"));
            return;
        }

        foreach (var entry in children)
        {
            var relative = prefix + entry.Name;
            if (entry.LinkTarget is not null)
            {
                found.Add(new ScanEntry(relative, entry.FullName, ScanEntryKind.Link));
            }
            else if (entry is DirectoryInfo child)
            {
                if (entry.Name != GitDirectory)
                {
                    Walk(child, relative + "/", found);
                }
            }
            else if (entry.Name != GitDirectory)
            {
                found.Add(new ScanEntry(relative, entry.FullName, ScanEntryKind.File));
            }
        }
    }
}
