using System.ComponentModel;
using System.Diagnostics;

namespace PandaBear.Testing.CodeFixRunner.Tests;

/// <summary>
/// Runs the built code-fix runner with <c>dotnet exec</c> in its own process, which has its own working directory and
/// MSBuild registration.
/// </summary>
internal static class CodeFixRunnerProcess
{
    private static readonly TimeSpan ExitTimeout = TimeSpan.FromSeconds(30);

    /// <summary>Runs the tool and waits at most 30 seconds for it to exit.</summary>
    public static async Task<CodeFixRunnerInvocationResult> Run(string workingDirectory, IReadOnlyList<string> args, CancellationToken ct)
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add("exec");
        startInfo.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "SharedKernel.Testing.CodeFixRunner.dll"));
        foreach (var arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("The code-fix runner did not start.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(ExitTimeout);
        var output = process.StandardOutput.ReadToEndAsync(timeout.Token);
        var error = process.StandardError.ReadToEndAsync(timeout.Token);
        try
        {
            await process.WaitForExitAsync(timeout.Token);
            return new CodeFixRunnerInvocationResult(process.ExitCode, await output, await error);
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
