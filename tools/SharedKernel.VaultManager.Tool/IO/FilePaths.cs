namespace PandaBear.VaultManager.Tool.IO;

/// <summary>Compares file paths by the file they reach rather than by how they are written.</summary>
internal static class FilePaths
{
    /// <summary>Most symbolic links followed while resolving one path, the limit Linux applies.</summary>
    public const int MaxLinks = 40;

    private static readonly char[] Separators = [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar];

    /// <summary>
    /// Whether two paths may name the same file: their full paths are equal once every symbolic link in them is resolved,
    /// ignoring case. Case is ignored on every platform because a case-sensitive system can mount a case-insensitive
    /// volume; two different files whose paths differ only in case are therefore treated as one.
    /// </summary>
    /// <param name="first">First path.</param>
    /// <param name="second">Second path.</param>
    /// <returns><see langword="true"/> when both paths may reach the same file.</returns>
    /// <exception cref="IOException">A path holds more than <see cref="MaxLinks"/> symbolic links.</exception>
    public static bool MayNameSameFile(string first, string second) =>
        string.Equals(ResolveLinks(first), ResolveLinks(second), StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Returns the full path with every symbolic link among its components replaced by the link's target, from the root
    /// down. The path is first made full as .NET does before it opens a file, which removes <c>..</c> by text. A link
    /// target is then followed one component at a time, so a <c>..</c> in it leaves the directory reached so far, as the
    /// operating system resolves it. Components that do not exist, or that cannot be inspected, are kept as written.
    /// </summary>
    /// <param name="path">Absolute or relative path.</param>
    /// <returns>The resolved full path.</returns>
    /// <exception cref="IOException">The path holds more than <see cref="MaxLinks"/> symbolic links.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="path"/> is empty.</exception>
    public static string ResolveLinks(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        string fullPath = Path.GetFullPath(path);
        string current = Path.GetPathRoot(fullPath) ?? string.Empty;
        var pending = new Stack<string>();
        Prepend(pending, fullPath[current.Length..]);
        int links = 0;
        while (pending.TryPop(out string? component))
        {
            if (component == ".")
            {
                continue;
            }

            if (component == "..")
            {
                current = Path.GetDirectoryName(current) ?? current;
                continue;
            }

            string next = Path.Join(current, component);
            if (LinkTarget(next) is not { } target)
            {
                current = next;
                continue;
            }

            if (++links > MaxLinks)
            {
                throw new IOException($"Path '{fullPath}' holds more than {MaxLinks} symbolic links.");
            }

            // A relative target continues from the link's directory, which is the current path.
            if (Path.IsPathRooted(target))
            {
                current = Path.GetPathRoot(target) ?? string.Empty;
                target = target[current.Length..];
            }

            Prepend(pending, target);
        }

        return current;
    }

    // Puts the components of a relative path on top of the stack, so the first one is resolved next.
    private static void Prepend(Stack<string> pending, string relativePath)
    {
        string[] components = relativePath.Split(Separators, StringSplitOptions.RemoveEmptyEntries);
        for (int index = components.Length - 1; index >= 0; index--)
        {
            pending.Push(components[index]);
        }
    }

    private static string? LinkTarget(string path)
    {
        try
        {
            return new FileInfo(path).LinkTarget;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }
}
