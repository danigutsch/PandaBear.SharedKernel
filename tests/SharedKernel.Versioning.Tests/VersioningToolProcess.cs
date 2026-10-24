using System.ComponentModel;
using System.Diagnostics;

namespace PandaBear.Versioning.Tests;

/// <summary>
/// Runs the built versioning tool with <c>dotnet exec</c> in its own process, which has its own working directory and
/// <c>PATH</c>.
/// </summary>
internal static class VersioningToolProcess
{
    private static readonly TimeSpan ExitTimeout = TimeSpan.FromSeconds(60);

    /// <summary>Runs the tool with <paramref name="path"/> as its <c>PATH</c> and waits at most 60 seconds for it to exit.</summary>
    public static async Task<CommandLineInvocation> Run(string workingDirectory, IReadOnlyList<string> args, string path, CancellationToken ct)
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add("exec");
        startInfo.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "SharedKernel.Versioning.Tool.dll"));
        foreach (var arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        startInfo.Environment["PATH"] = path;
        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("The versioning tool did not start.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(ExitTimeout);
        var output = process.StandardOutput.ReadToEndAsync(timeout.Token);
        var error = process.StandardError.ReadToEndAsync(timeout.Token);
        try
        {
            await process.WaitForExitAsync(timeout.Token);
            return new CommandLineInvocation(process.ExitCode, await output, await error);
        }
        catch (OperationCanceledException)
        {
            await Stop(process);
            throw;
        }
    }

    // Best effort: the cancellation that led here is what the caller sees.
    private static async Task Stop(Process process)
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
