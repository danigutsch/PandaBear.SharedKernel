using System.Globalization;

namespace PandaBear.RepoConfig.Tests;

/// <summary>Waits for a child process to report a process ID through a file.</summary>
internal static class ChildProcessId
{
    /// <summary>Polls <paramref name="path"/> until it holds a process ID, for at most 30 seconds, and fails when <paramref name="running"/> completes first.</summary>
    public static async Task<int> Read(string path, Task running, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        while (!running.IsCompleted)
        {
            var processId = await TryRead(path, timeout.Token);
            if (processId is not null)
            {
                return processId.Value;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(20), timeout.Token);
        }

        await running;
        throw new InvalidOperationException("The child process exited before reporting a process ID.");
    }

    private static async Task<int?> TryRead(string path, CancellationToken ct)
    {
        try
        {
            var text = await File.ReadAllTextAsync(path, ct);
            return int.TryParse(text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var processId) ? processId : null;
        }
        catch (IOException)
        {
            // The file is missing or still being written; poll again.
            return null;
        }
    }
}
