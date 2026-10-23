using PandaBear.VaultManager.Tool.IO;

namespace PandaBear.VaultManager.Tool.Cli;

/// <summary>Checks the files a command writes before it does any work.</summary>
internal static class OutputFiles
{
    // Case is ignored on every platform, so paths that differ only in case are refused even where they name two files.
    private const string ReportFile = "report file";

    private const string SameFileRule = "paths are compared after resolving symbolic links and ignoring case";

    /// <summary>
    /// Checks that a file can be written: it is no input and no other file the command writes, it is not a directory,
    /// its directory exists, and it does not exist unless it may be replaced. Paths are compared by the file they may
    /// reach, as <see cref="FilePaths.MayNameSameFile"/> does, so a symbolic link or a different case does not hide an
    /// input. Writes the problem to <paramref name="error"/>.
    /// </summary>
    /// <param name="path">File to write.</param>
    /// <param name="name">What the file is, such as <c>output file</c>.</param>
    /// <param name="inputs">Input files, which are never written.</param>
    /// <param name="others">Other files the command writes, with what each is.</param>
    /// <param name="replaceExisting">Whether an existing file may be replaced.</param>
    /// <param name="error">Receives the problem.</param>
    /// <returns><see langword="true"/> when the file can be written.</returns>
    /// <exception cref="IOException">A path holds more than <see cref="FilePaths.MaxLinks"/> symbolic links.</exception>
    public static bool CanWrite(string path, string name, IReadOnlyList<string> inputs, IReadOnlyList<(string Path, string Name)> others, bool replaceExisting, TextWriter error)
    {
        string fullPath = Path.GetFullPath(path);
        string? problem =
            inputs.Any(input => FilePaths.MayNameSameFile(input, fullPath)) ? $"The {name} '{fullPath}' may be the same file as an input file: {SameFileRule}. Choose another path."
            : others.FirstOrDefault(other => FilePaths.MayNameSameFile(other.Path, fullPath)) is { Name: { } other } ? $"The {name} '{fullPath}' may be the same file as the {other}: {SameFileRule}. Choose another path."
            : Directory.Exists(fullPath) ? $"The {name} '{fullPath}' is a directory. Choose a file path."
            : !Directory.Exists(Path.GetDirectoryName(fullPath)) ? $"The directory of the {name} '{fullPath}' does not exist."
            : !replaceExisting && File.Exists(fullPath) ? $"The {name} '{fullPath}' already exists. Choose another path or pass --force to replace it."
            : null;
        if (problem is not null)
        {
            error.WriteLine(PrintableText.OneLine(problem));
        }

        return problem is null;
    }

    /// <summary>
    /// Checks that the Markdown report can be written, as <see cref="CanWrite"/> does, and that an existing file it would
    /// replace is a vault report, so a mistyped path cannot replace a file such as a decisions file.
    /// </summary>
    /// <param name="path">Report file.</param>
    /// <param name="inputs">Input files, which are never written.</param>
    /// <param name="others">Other files the command writes, with what each is.</param>
    /// <param name="error">Receives the problem.</param>
    /// <returns><see langword="true"/> when the report can be written.</returns>
    /// <exception cref="IOException">A path holds more than <see cref="FilePaths.MaxLinks"/> symbolic links.</exception>
    public static bool CanWriteReport(string path, IReadOnlyList<string> inputs, IReadOnlyList<(string Path, string Name)> others, TextWriter error)
    {
        if (!CanWrite(path, ReportFile, inputs, others, replaceExisting: true, error))
        {
            return false;
        }

        string fullPath = Path.GetFullPath(path);
        if (File.Exists(fullPath) && !IsVaultReport(fullPath))
        {
            error.WriteLine(PrintableText.OneLine($"The {ReportFile} '{fullPath}' exists and is not a vault report. Choose another path, or delete the file first."));
            return false;
        }

        return true;
    }

    // A report starts with the heading VaultReport writes. A file that cannot be read counts as no report.
    private static bool IsVaultReport(string fullPath)
    {
        ReadOnlySpan<byte> heading = "# Vault report\n"u8;
        Span<byte> start = stackalloc byte[heading.Length];
        try
        {
            using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return stream.ReadAtLeast(start, start.Length, throwOnEndOfStream: false) == start.Length && start.SequenceEqual(heading);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
