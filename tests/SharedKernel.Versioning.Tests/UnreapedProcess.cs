using System.Diagnostics;
using System.Globalization;

namespace PandaBear.Versioning.Tests;

/// <summary>A Linux background process whose parent never reaps it, so its exit is not observable after it is killed.</summary>
internal sealed class UnreapedProcess : IDisposable
{
    private readonly Process _parent;

    private UnreapedProcess(Process parent, Process process)
    {
        _parent = parent;
        Process = process;
    }

    /// <summary>
    /// Gets the background sleep process.
    /// </summary>
    public Process Process
    {
        get;
    }

    /// <summary>Gets whether the process has terminated and still waits for its parent to reap it.</summary>
    public bool IsZombie
    {
        get
        {
            var status = File.ReadAllText($"/proc/{Process.Id}/stat");
            return status[status.LastIndexOf(')') + 2] == 'Z';
        }
    }

    /// <summary>Starts a shell that prints the ID of a background <c>sleep</c> and replaces itself with another <c>sleep</c>.</summary>
    public static async Task<UnreapedProcess> Start(CancellationToken ct)
    {
        var startInfo = new ProcessStartInfo("/bin/sh")
        {
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add("-c");
        startInfo.ArgumentList.Add("sleep 60 & echo $!; exec sleep 60");
        var parent = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start /bin/sh.");
        try
        {
            var processId = await parent.StandardOutput.ReadLineAsync(ct)
                ?? throw new InvalidOperationException("The shell did not print the background process ID.");
            return new UnreapedProcess(parent, Process.GetProcessById(int.Parse(processId, CultureInfo.InvariantCulture)));
        }
        catch
        {
            Stop(parent);
            throw;
        }
    }

    /// <summary>
    /// Disposes the process and kills its parent shell.
    /// </summary>
    public void Dispose()
    {
        Process.Dispose();
        Stop(_parent);
    }

    private static void Stop(Process parent)
    {
        parent.Kill(entireProcessTree: true);
        parent.StandardOutput.Dispose();
        parent.Dispose();
    }
}
