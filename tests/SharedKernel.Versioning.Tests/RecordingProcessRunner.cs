namespace PandaBear.Versioning.Tests;

/// <summary>
/// Records the processes that a command would start instead of starting them. The process whose file name, without its
/// extension, is <paramref name="failingFileName" /> fails as <see cref="Tool.CommandRunner" /> reports a non-zero exit.
/// </summary>
internal sealed class RecordingProcessRunner(string? failingFileName = null)
{
    /// <summary>
    /// Gets each process in start order, with the content of its <c>--configfile</c> and the entries of its working
    /// directory when it started.
    /// </summary>
    public List<(string FileName, string[] Arguments, string? WorkingDirectory, string? Configuration, string[] WorkingDirectoryEntries)> Processes
    {
        get;
    } = [];

    /// <summary>
    /// Records the process with its arguments, configuration file content, and working directory entries, then fails when its name matches the failing file name or returns a fixed output line.
    /// </summary>
    public async Task<string> Run(string fileName, IEnumerable<string> arguments, string? workingDirectory, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var argumentList = arguments.ToArray();
        var configuration = Array.IndexOf(argumentList, "--configfile");
        Processes.Add((
            fileName,
            argumentList,
            workingDirectory,
            configuration < 0 ? null : await File.ReadAllTextAsync(argumentList[configuration + 1], ct),
            workingDirectory is null ? [] : Directory.GetFileSystemEntries(workingDirectory)));
        if (string.Equals(Path.GetFileNameWithoutExtension(fileName), failingFileName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Command failed (3): {fileName} {string.Join(' ', argumentList)}");
        }

        return fileName + " ran" + Environment.NewLine;
    }
}
