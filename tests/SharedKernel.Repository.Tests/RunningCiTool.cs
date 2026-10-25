using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.Versioning;

namespace PandaBear.Repository.Tests;

/// <summary>
/// A <c>dotnet exec</c> of the built CI tool that a test started in a process of its own. The test can signal it and wait
/// for its exit; disposing it kills the process tree if the tool still runs.
/// </summary>
/// <param name="process">The tool process, whose standard output and standard error are redirected.</param>
internal sealed class RunningCiTool(Process process) : IDisposable
{
    private static readonly TimeSpan ExitTimeout = TimeSpan.FromSeconds(60);

    private readonly Task<string> output = process.StandardOutput.ReadToEndAsync();
    private readonly Task<string> error = process.StandardError.ReadToEndAsync();

    /// <summary>Gets whether the tool has exited.</summary>
    public bool HasExited => process.HasExited;

    /// <summary>Waits at most 60 seconds for the tool to exit and returns what it wrote.</summary>
    /// <param name="ct">Cancels the wait, which kills the tool.</param>
    public async Task<RepositoryToolResult> Exited(CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(ExitTimeout);
        try
        {
            await process.WaitForExitAsync(timeout.Token);
            return new RepositoryToolResult(process.ExitCode, await output.WaitAsync(timeout.Token), await error.WaitAsync(timeout.Token));
        }
        catch (OperationCanceledException)
        {
            await Stop();
            throw;
        }
    }

    /// <summary>Sends SIGTERM to the tool, as a cancelled CI job or a service manager does.</summary>
    /// <param name="ct">Cancels the wait for the signal to be sent.</param>
    [UnsupportedOSPlatform("windows")]
    public async Task Terminate(CancellationToken ct)
    {
        var startInfo = new ProcessStartInfo("/bin/sh") { UseShellExecute = false };
        startInfo.ArgumentList.Add("-c");
        startInfo.ArgumentList.Add("kill -TERM \"$0\"");
        startInfo.ArgumentList.Add(process.Id.ToString(System.Globalization.CultureInfo.InvariantCulture));
        using var kill = Process.Start(startInfo) ?? throw new InvalidOperationException("The kill command did not start.");
        await kill.WaitForExitAsync(ct);
        kill.ExitCode.ShouldBe(0, "The tool must still run when the signal is sent.");
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Stop().GetAwaiter().GetResult();
        process.Dispose();
    }

    // Best effort: the failure that led here is what the caller sees.
    private async Task Stop()
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                using var terminationTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                await process.WaitForExitAsync(terminationTimeout.Token);
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException
            or Win32Exception
            or NotSupportedException
            or AggregateException
            or OperationCanceledException)
        {
        }
    }
}
