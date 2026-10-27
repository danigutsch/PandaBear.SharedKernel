using System.Diagnostics;

namespace PandaBear.K6.Tool.Tests;

/// <summary>Reports whether a process that a fake executable recorded is still running.</summary>
internal static class ProcessState
{
    /// <summary>Returns whether a process with the ID exists and has not exited.</summary>
    public static bool IsRunning(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
