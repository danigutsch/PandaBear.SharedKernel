namespace PandaBear.Processes;

/// <summary>Searches the absolute directories of a <c>PATH</c> value for an executable file.</summary>
internal static class ExecutableSearch
{
    /// <summary>Returns the first executable named <paramref name="command"/> in an absolute directory of <paramref name="path"/>.</summary>
    internal static string? Find(string command, string? path, bool isWindows)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var fileNames = isWindows
            ? new[] { $"{command}.exe", command }
            : new[] { command };

        foreach (var directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var absoluteDirectory = directory.Trim('"');
            if (!Path.IsPathFullyQualified(absoluteDirectory))
            {
                continue;
            }

            foreach (var fileName in fileNames)
            {
                var candidate = Path.Combine(absoluteDirectory, fileName);
                if (File.Exists(candidate) && IsExecutable(candidate, isWindows))
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    /// <summary>Returns whether the file mode grants execute permission to the user, group, or others.</summary>
    internal static bool HasUnixExecutePermission(UnixFileMode mode) =>
        (mode & (UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute)) != 0;

    private static bool IsExecutable(string path, bool isWindows)
    {
        if (OperatingSystem.IsWindows() || isWindows)
        {
            return true;
        }

        try
        {
            var mode = File.GetUnixFileMode(path);
            return HasUnixExecutePermission(mode);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
