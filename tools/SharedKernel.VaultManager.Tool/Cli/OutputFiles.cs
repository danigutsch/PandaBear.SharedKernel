namespace PandaBear.VaultManager.Tool.Cli;

/// <summary>Checks the files a command writes before it does any work.</summary>
internal static class OutputFiles
{
    /// <summary>
    /// Checks that a file can be written: it is no input and no other file the command writes, it is not a directory,
    /// its directory exists, and it does not exist unless it may be replaced. Writes the problem to
    /// <paramref name="error"/>.
    /// </summary>
    /// <param name="path">File to write.</param>
    /// <param name="name">What the file is, such as <c>output file</c>.</param>
    /// <param name="inputs">Input files, which are never written.</param>
    /// <param name="others">Other files the command writes, with what each is.</param>
    /// <param name="replaceExisting">Whether an existing file may be replaced.</param>
    /// <param name="error">Receives the problem.</param>
    /// <returns><see langword="true"/> when the file can be written.</returns>
    public static bool CanWrite(string path, string name, IReadOnlyList<string> inputs, IReadOnlyList<(string Path, string Name)> others, bool replaceExisting, TextWriter error)
    {
        string fullPath = Path.GetFullPath(path);
        string? problem =
            inputs.Any(input => Same(input, fullPath)) ? $"The {name} '{fullPath}' is also an input file. Choose another path."
            : others.FirstOrDefault(other => Same(other.Path, fullPath)) is { Name: { } other } ? $"The {name} '{fullPath}' is also the {other}. Choose another path."
            : Directory.Exists(fullPath) ? $"The {name} '{fullPath}' is a directory. Choose a file path."
            : !Directory.Exists(Path.GetDirectoryName(fullPath)) ? $"The directory of the {name} '{fullPath}' does not exist."
            : !replaceExisting && File.Exists(fullPath) ? $"The {name} '{fullPath}' already exists. Choose another path or pass --force to replace it."
            : null;
        if (problem is not null)
        {
            error.WriteLine(problem);
        }

        return problem is null;
    }

    // Compares full paths like the pipeline does: without case on Windows and macOS, whose file systems usually ignore it.
    private static bool Same(string first, string second) => string.Equals(
        Path.GetFullPath(first),
        Path.GetFullPath(second),
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
}
