using System.Diagnostics;
using PandaBear.Processes;

namespace PandaBear.Versioning.Tests;

/// <summary>
/// Records the processes that a command would start instead of starting them, and resolves every bare command name to a
/// path in a directory that does not exist. The process whose file name, without its extension, is
/// <paramref name="failingFileName" /> exits with code 3, which <see cref="ExternalCommands" /> reports as a failure.
/// </summary>
internal sealed class RecordingProcessRunner(string? failingFileName = null) : IProcessRunner, IExecutableResolver
{
    private static readonly string ResolvedDirectory = Path.Combine(Path.GetTempPath(), "recorded-commands-" + Guid.NewGuid().ToString("N"));

    /// <summary>
    /// Gets each process in start order, with the content of its <c>--configfile</c> and the entries of its working
    /// directory when it started. A resolved bare command keeps its bare name.
    /// </summary>
    public List<(string FileName, string[] Arguments, string? WorkingDirectory, string? Configuration, string[] WorkingDirectoryEntries)> Processes { get; } = [];

    /// <summary>Gets external commands that start their processes through this runner.</summary>
    public ExternalCommands Commands => new(this, this);

    /// <inheritdoc />
    public string? Find(string command) => Path.Combine(ResolvedDirectory, command);

    /// <summary>
    /// Records the process with its arguments, configuration file content, and working directory entries, then fails when
    /// its name matches the failing file name or returns a fixed output line.
    /// </summary>
    public async Task<ProcessOutput> Run(ProcessStartInfo startInfo, TextWriter? forward, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var fileName = Path.GetDirectoryName(startInfo.FileName) == ResolvedDirectory ? Path.GetFileName(startInfo.FileName) : startInfo.FileName;
        var argumentList = startInfo.ArgumentList.ToArray();
        var workingDirectory = string.IsNullOrEmpty(startInfo.WorkingDirectory) ? null : startInfo.WorkingDirectory;
        var configuration = Array.IndexOf(argumentList, "--configfile");
        Processes.Add((
            fileName,
            argumentList,
            workingDirectory,
            configuration < 0 ? null : await File.ReadAllTextAsync(argumentList[configuration + 1], cancellationToken),
            workingDirectory is null ? [] : Directory.GetFileSystemEntries(workingDirectory)));
        return string.Equals(Path.GetFileNameWithoutExtension(fileName), failingFileName, StringComparison.Ordinal)
            ? new ProcessOutput(3, string.Empty)
            : new ProcessOutput(0, startInfo.FileName + " ran" + Environment.NewLine);
    }
}
