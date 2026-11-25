using System.Diagnostics;

namespace SharedKernel.RepoConfig.Tests;

/// <summary>Runs Git against the scaffolded repository and captures hook output.</summary>
internal static class HookProfileTestOperations
{
    /// <summary>Runs Git and optionally puts the directory with a fixture executable first on the tool path.</summary>
    public static async Task<(int ExitCode, string Output, string Error)> RunGit(
        TemporaryRepoConfigWorkspace workspace,
        string[] arguments,
        string? toolDirectory = null)
    {
        ProcessStartInfo startInfo = new("git")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = workspace.RootPath,
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        startInfo.Environment["LC_ALL"] = "C";
        if (toolDirectory is not null)
        {
            startInfo.Environment["PATH"] = toolDirectory + Path.PathSeparator + startInfo.Environment["PATH"];
        }

        using var process = Process.Start(startInfo)!;
        var cancellationToken = TestContext.Current.CancellationToken;
        var output = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var error = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        return (process.ExitCode, await output, await error);
    }
}
