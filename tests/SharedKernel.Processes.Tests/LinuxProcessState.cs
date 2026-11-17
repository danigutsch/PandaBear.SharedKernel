using System.Globalization;

namespace PandaBear.Processes.Tests;

/// <summary>Reads whether a Linux process still runs from <c>/proc</c>.</summary>
internal static class LinuxProcessState
{
    /// <summary>
    /// Waits until the process has stopped running: it no longer exists, or it is a zombie that its new parent has not
    /// reaped yet. Returns whether it stopped within <paramref name="limit"/>.
    /// </summary>
    public static async Task<bool> WaitUntilStopped(int processId, TimeSpan limit, CancellationToken ct)
    {
        for (var waited = TimeSpan.Zero; waited < limit; waited += TimeSpan.FromMilliseconds(20))
        {
            if (!IsRunning(processId))
            {
                return true;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(20), ct);
        }

        return !IsRunning(processId);
    }

    private static bool IsRunning(int processId)
    {
        try
        {
            var status = File.ReadAllText($"/proc/{processId.ToString(CultureInfo.InvariantCulture)}/stat");
            return status[status.AsSpan().LastIndexOf(')') + 2] != 'Z';
        }
        catch (IOException)
        {
            // The process is gone: its /proc entry no longer exists or vanished during the read (ESRCH).
            return false;
        }
    }
}
