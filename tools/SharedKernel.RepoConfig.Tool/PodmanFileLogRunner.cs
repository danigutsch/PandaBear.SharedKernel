using System.Diagnostics;
using SharedKernel.Processes;

namespace SharedKernel.RepoConfig.Tool;

/// <summary>Runs the isolated child with its two byte streams written directly to the caller's log files.</summary>
internal sealed class PodmanFileLogRunner(TimeSpan timeout, Stream output, Stream error, long maxBytes) : IProcessRunner
{
    private readonly BoundedProcessRunner _runner = new(timeout);

    /// <inheritdoc />
    public async Task<ProcessOutput> Run(ProcessStartInfo startInfo, TextWriter? forward, CancellationToken cancellationToken)
    {
        var code = await _runner.RunToStreams(startInfo, output, maxBytes, error, maxBytes, cancellationToken).ConfigureAwait(false);
        return new ProcessOutput(code, string.Empty);
    }

    /// <summary>Resolves an existing absolute directory for the output files.</summary>
    /// <exception cref="ArgumentException">The directory is missing, relative, or a symbolic link.</exception>
    public static string ResolveDirectory(string directory)
    {
        var path = Path.IsPathFullyQualified(directory) ? Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory)) : null;
        if (path is null || !Directory.Exists(path) || File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint))
        {
            throw new ArgumentException("The log directory must be an existing absolute directory and cannot be a symbolic link.", nameof(directory));
        }
        return path;
    }

    /// <summary>Creates a new log, with unbuffered writes so partial output remains available on failure.</summary>
    public static FileStream OpenLog(string path)
    {
        var options = new FileStreamOptions
        {
            Mode = FileMode.CreateNew,
            Access = FileAccess.Write,
            Share = FileShare.Read,
            Options = FileOptions.Asynchronous,
            BufferSize = 1,
        };
        if (!OperatingSystem.IsWindows())
        {
            options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        }
        return new FileStream(path, options);
    }
}
