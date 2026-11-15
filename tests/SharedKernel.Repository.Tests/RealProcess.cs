using PandaBear.Processes;

namespace PandaBear.Repository.Tests;

/// <summary>Runs real processes, such as Git, for tests that need a real checkout.</summary>
internal static class RealProcess
{
    /// <summary>Runs <paramref name="fileName"/>, found on the absolute <c>PATH</c> directories, and returns its standard output.</summary>
    /// <exception cref="InvalidOperationException">The process exited with a non-zero code.</exception>
    public static async Task<string> Run(string fileName, string[] arguments, string workingDirectory, CancellationToken ct)
    {
        var startInfo = new System.Diagnostics.ProcessStartInfo(PathExecutableResolver.Default.Resolve(fileName))
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            WorkingDirectory = workingDirectory,
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        var result = await new SystemProcessRunner().Run(startInfo, null, ct);
        return result.ExitCode == 0
            ? result.StandardOutput
            : throw new InvalidOperationException(
                $"Command failed ({result.ExitCode}): {fileName} {string.Join(' ', arguments)}{Environment.NewLine}{result.StandardOutput}{Environment.NewLine}{result.StandardError}");
    }
}
