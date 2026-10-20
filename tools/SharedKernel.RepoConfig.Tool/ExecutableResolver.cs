namespace SharedKernel.RepoConfig.Tool;

internal static class ExecutableResolver
{
    internal static string? Resolve(string command, string? path, bool isWindows)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
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

    internal static bool HasUnixExecutePermission(UnixFileMode mode) =>
        (mode & (UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute)) != 0;
}
