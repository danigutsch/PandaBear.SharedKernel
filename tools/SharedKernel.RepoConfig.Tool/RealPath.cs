namespace PandaBear.RepoConfig.Tool;

/// <summary>
/// Resolves the symbolic links in a path the way Python's <c>os.path.realpath</c> does.
/// </summary>
internal static class RealPath
{
    private const int MaxLinks = 40;
    private static readonly char[] Separators = [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar];

    /// <summary>
    /// Resolves every symbolic link in the absolute <paramref name="path"/>, component by component. A component that
    /// does not exist is kept, and <c>..</c> applies to the resolved parent rather than to the path as written.
    /// </summary>
    public static string Resolve(string path)
    {
        var resolved = Path.GetPathRoot(path) ?? throw new ArgumentException("The path must be absolute.", nameof(path));
        Stack<string> pending = new();
        Push(pending, path[resolved.Length..]);
        var links = 0;
        while (pending.TryPop(out var name))
        {
            if (name is "" or ".")
            {
                continue;
            }

            if (name == "..")
            {
                resolved = Path.GetDirectoryName(resolved) ?? resolved;
                continue;
            }

            var candidate = Path.Join(resolved, name);
            var target = LinkTarget(candidate);

            // A link loop leaves the remaining components unresolved, as a non-strict realpath does.
            if (target is null || ++links > MaxLinks)
            {
                resolved = candidate;
                continue;
            }

            if (Path.IsPathRooted(target))
            {
                resolved = Path.GetPathRoot(target)!;
                target = target[resolved.Length..];
            }

            Push(pending, target);
        }

        return resolved;
    }

    private static string? LinkTarget(string path)
    {
        try
        {
            return new FileInfo(path).LinkTarget;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            // Like realpath, a component that cannot be inspected is not a link.
            return null;
        }
    }

    private static void Push(Stack<string> pending, string relativePath)
    {
        var names = relativePath.Split(Separators);
        for (var index = names.Length - 1; index >= 0; index--)
        {
            pending.Push(names[index]);
        }
    }
}
