using System.Diagnostics;

namespace PandaBear.RepoConfig.Tests;

/// <summary>A fake scanner process that records its start info, optionally writes a report file, and returns a fixed exit code.</summary>
/// <param name="exitCode">The exit code the fake process returns.</param>
/// <param name="report">The text written to the <c>--report-path</c> file, or <see langword="null"/> to write nothing.</param>
internal sealed class SecurityScannerTestProcess(int exitCode, string? report)
{
    /// <summary>Gets the start info passed to the most recent <see cref="Run"/> call.</summary>
    public ProcessStartInfo? StartInfo
    {
        get; private set;
    }

    /// <summary>Records <paramref name="startInfo"/>, throws if cancelled, writes the report to the <c>--report-path</c> argument when one was given, and returns the exit code.</summary>
    /// <param name="startInfo">The scanner process start info.</param>
    /// <param name="cancellationToken">The token checked before the fake run.</param>
    public async Task<int> Run(ProcessStartInfo startInfo, CancellationToken cancellationToken)
    {
        StartInfo = startInfo;
        cancellationToken.ThrowIfCancellationRequested();
        if (report is not null)
        {
            var index = startInfo.ArgumentList.IndexOf("--report-path");
            await File.WriteAllTextAsync(startInfo.ArgumentList[index + 1], report, cancellationToken);
        }

        return exitCode;
    }
}
