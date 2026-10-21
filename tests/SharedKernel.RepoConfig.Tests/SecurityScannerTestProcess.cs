using System.Diagnostics;

namespace PandaBear.RepoConfig.Tests;

internal sealed class SecurityScannerTestProcess(int exitCode, string? report)
{
    public ProcessStartInfo? StartInfo { get; private set; }

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
