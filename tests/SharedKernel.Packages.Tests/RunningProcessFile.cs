using System.Diagnostics;

namespace PandaBear.Packages.Tests;

/// <summary>Reads the process ID that a test script wrote to a file and checks whether that process still runs.</summary>
internal static class RunningProcessFile
{
    /// <summary>Waits until the script has written its ID, then returns it.</summary>
    /// <exception cref="TimeoutException">No complete ID appeared within ten seconds.</exception>
    public static async Task<int> WaitForProcessId(string path, CancellationToken cancellationToken)
    {
        var deadline = TimeProvider.System.GetUtcNow().AddSeconds(10);
        while (TimeProvider.System.GetUtcNow() < deadline)
        {
            if (File.Exists(path) && int.TryParse((await File.ReadAllTextAsync(path, cancellationToken)).Trim(), out var id))
            {
                return id;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(20), cancellationToken);
        }

        throw new TimeoutException("The script did not write its process ID.");
    }

    /// <summary>Returns whether a process with <paramref name="id"/> still exists.</summary>
    public static bool IsRunning(int id)
    {
        try
        {
            using var process = Process.GetProcessById(id);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
