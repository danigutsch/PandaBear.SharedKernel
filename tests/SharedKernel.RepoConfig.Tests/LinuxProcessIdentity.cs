using System.Globalization;

namespace PandaBear.RepoConfig.Tests;

/// <summary>Identifies a Linux process by its ID and start time, so a reused process ID never matches it.</summary>
/// <param name="ProcessId">The process ID.</param>
/// <param name="StartTime">The start time from <c>/proc/[pid]/stat</c>, in clock ticks after boot.</param>
internal sealed record LinuxProcessIdentity(int ProcessId, ulong StartTime)
{
    /// <summary>Captures the identity of a running process.</summary>
    public static LinuxProcessIdentity Capture(int processId) =>
        TryReadLiveStartTime(processId) is { } startTime
            ? new LinuxProcessIdentity(processId, startTime)
            : throw new InvalidOperationException($"Process {processId} is not running.");

    /// <summary>Polls until the process exits, for at most 30 seconds.</summary>
    /// <returns><see langword="true"/> when the process exited; <see langword="false"/> when it still runs after 30 seconds.</returns>
    /// <remarks>A zombie has exited, so it counts as exited before its parent reaps it.</remarks>
    public async Task<bool> WaitForExit(CancellationToken ct)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        while (IsRunning())
        {
            if (deadline.IsCancellationRequested)
            {
                return false;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(20), ct);
        }

        return true;
    }

    private bool IsRunning() => TryReadLiveStartTime(ProcessId) == StartTime;

    private static ulong? TryReadLiveStartTime(int processId)
    {
        string stat;
        try
        {
            stat = File.ReadAllText(Path.Combine("/proc", processId.ToString(CultureInfo.InvariantCulture), "stat"));
        }
        catch (IOException)
        {
            // The process exited and its procfs entry is gone.
            return null;
        }

        // The command name can contain spaces and parentheses, so fields are counted after its closing parenthesis.
        // The first of them is the state; the start time is the twentieth.
        var fields = stat[(stat.LastIndexOf(')') + 2)..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return fields[0] is "Z" or "X"
            ? null
            : ulong.Parse(fields[19], NumberStyles.None, CultureInfo.InvariantCulture);
    }
}
