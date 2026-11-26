using System.Diagnostics;
using SharedKernel.Processes;
using static SharedKernel.RepoConfig.Tool.ResourceBudgetGuard;

namespace SharedKernel.RepoConfig.Tests;

/// <summary>Waits for the real owned shell to start its descendant and joins its cleanup.</summary>
internal static class ResourceBudgetProcessFixture
{
    private const string DescendantCommand = """/bin/sleep 30 & printf '%s\n' "$!" > "$1"; wait""";
    private static readonly TimeSpan ExitTimeout = TimeSpan.FromSeconds(2);

    /// <summary>Opens an owned child's stat entry, reaps the child, and transfers the retained descriptor.</summary>
    /// <returns>The open descriptor, which the caller disposes.</returns>
    internal static async Task<FileStream> OpenReapedStat()
    {
        using var child = ProcessTree.Start(new ProcessStartInfo("/bin/sleep")
        {
            ArgumentList = { "30" },
            UseShellExecute = false,
        });
        FileStream? stat = null;
        try
        {
            stat = File.OpenRead(FormattableString.Invariant($"/proc/{child.Id}/stat"));
            await ProcessTree.Terminate(child, ExitTimeout);
            child.HasExited.ShouldBeTrue("The child must be reaped while its stat descriptor stays open.");
            return stat;
        }
        catch
        {
            await ProcessTree.Terminate(child, ExitTimeout);
            if (stat is not null)
            {
                await stat.DisposeAsync();
            }

            throw;
        }
    }

    /// <summary>Stops the command after its marker confirms the descendant was launched.</summary>
    internal static Task<ChildResult> RunUntilDescendantStarts(string directory, CancellationToken ct) =>
        RunUntilMarker(directory, (ready, token) =>
        {
            var startInfo = new ProcessStartInfo("/bin/sh") { WorkingDirectory = directory, UseShellExecute = false };
            foreach (var argument in new[] { "-c", DescendantCommand, "fixture", ready })
            {
                startInfo.ArgumentList.Add(argument);
            }

            return ResourceBudgetProcess.Run(startInfo, token);
        }, ct);

    /// <summary>Stops the CLI invocation after the owned shell writes its descendant marker.</summary>
    internal static Task<int> RunBudgetCommandUntilDescendantStarts(string directory, string reportPath, CancellationToken ct) =>
        RunUntilMarker(directory, (ready, token) => RepoConfigToolRunner.Run(
            [
                "run-with-budget", "--working-directory", directory, "--scratch", directory, "--report", reportPath,
                "--minimum-host-memory-mib", "1", "--minimum-scratch-mib", "1", "--sample-seconds", "1",
                "--", "/bin/sh", "-c", DescendantCommand, "fixture", ready,
            ],
            TextWriter.Null, TextWriter.Null, directory, token), ct);

    private static async Task<T> RunUntilMarker<T>(string directory, Func<string, CancellationToken, Task<T>> start, CancellationToken ct)
    {
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(ct);
        stop.CancelAfter(TimeSpan.FromSeconds(10));
        var ready = Path.Combine(directory, "descendant-ready");
        var child = start(ready, stop.Token);
        try
        {
            while (!File.Exists(ready))
            {
                await Task.Delay(TimeSpan.FromMilliseconds(20), stop.Token);
            }
        }
        finally
        {
            await stop.CancelAsync();
            await child;
        }

        return await child;
    }
}
