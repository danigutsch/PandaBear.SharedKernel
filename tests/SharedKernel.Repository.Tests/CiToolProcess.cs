using System.ComponentModel;
using System.Diagnostics;

namespace PandaBear.Repository.Tests;

/// <summary>
/// Runs the built CI tool with <c>dotnet exec</c> in its own process, which has its own working directory and
/// environment.
/// </summary>
internal static class CiToolProcess
{
    private static readonly TimeSpan ExitTimeout = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Runs the tool with <paramref name="environment"/> added to the inherited environment and waits at most 60 seconds
    /// for it to exit.
    /// </summary>
    public static async Task<RepositoryToolResult> Run(
        string workingDirectory,
        IReadOnlyList<string> args,
        IReadOnlyDictionary<string, string?> environment,
        CancellationToken ct)
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add("exec");
        startInfo.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "SharedKernel.CI.Tool.dll"));
        foreach (var arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        foreach (var (name, value) in environment)
        {
            startInfo.Environment[name] = value;
        }

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("The CI tool did not start.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(ExitTimeout);
        var output = process.StandardOutput.ReadToEndAsync(timeout.Token);
        var error = process.StandardError.ReadToEndAsync(timeout.Token);
        try
        {
            await process.WaitForExitAsync(timeout.Token);
            return new RepositoryToolResult(process.ExitCode, await output, await error);
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
