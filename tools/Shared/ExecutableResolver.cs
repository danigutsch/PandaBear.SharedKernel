namespace PandaBear.Tooling;

/// <summary>
/// Resolves a command to an executable file in an absolute <c>PATH</c> directory. A bare file name passed to
/// <see cref="System.Diagnostics.Process"/> is also looked up in the application and current directories, and relative
/// <c>PATH</c> entries resolve from the current directory, so a repository could otherwise supply the executable.
/// </summary>
internal static class ExecutableResolver
{
    /// <summary>
    /// Returns <paramref name="fileName"/> when it is a fully qualified path, otherwise the executable that
    /// <see cref="Resolve"/> finds in the absolute directories of the process <c>PATH</c>.
    /// </summary>
    /// <param name="fileName">A command name or a fully qualified executable path.</param>
    /// <returns>The absolute executable path to start.</returns>
    /// <exception cref="InvalidOperationException">No absolute <c>PATH</c> directory contains the command.</exception>
    internal static string ResolveCommand(string fileName) =>
        Path.IsPathFullyQualified(fileName)
            ? fileName
            : Resolve(fileName, Environment.GetEnvironmentVariable("PATH"), OperatingSystem.IsWindows())
                ?? throw new InvalidOperationException($"Failed to start {fileName}: no absolute PATH directory contains it.");

    /// <summary>Returns the first executable named <paramref name="command"/> in an absolute directory of <paramref name="path"/>.</summary>
    /// <param name="command">The command name, without an extension.</param>
    /// <param name="path">The directory list, separated by <see cref="Path.PathSeparator"/>.</param>
    /// <param name="isWindows">Whether to try <c>&lt;command&gt;.exe</c> first and skip the Unix execute-permission check.</param>
    /// <returns>The absolute executable path, or <see langword="null"/> when no absolute directory contains it.</returns>
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

    /// <summary>Returns whether the file mode grants execute permission to the user, group, or others.</summary>
    internal static bool HasUnixExecutePermission(UnixFileMode mode) =>
        (mode & (UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute)) != 0;
}
