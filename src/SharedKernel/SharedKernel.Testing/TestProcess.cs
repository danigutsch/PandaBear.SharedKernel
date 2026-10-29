using System.ComponentModel;
using System.Diagnostics;

namespace PandaBear.Testing;

/// <summary>Runs external processes for tests.</summary>
public static class TestProcess
{
    /// <summary>Runs a process to completion, capturing standard output and standard error, and kills its process tree when <paramref name="cancellationToken" /> is canceled.</summary>
    /// <param name="fileName">The executable to start.</param>
    /// <param name="workingDirectory">The working directory of the process.</param>
    /// <param name="arguments">The arguments passed without shell interpretation.</param>
    /// <param name="cancellationToken">Cancels the wait and kills the process.</param>
    /// <param name="environment">Environment variables to set or, with a <see langword="null" /> value, to remove.</param>
    /// <returns>The exit code and captured output.</returns>
    public static async Task<TestProcessResult> Run(
        string fileName,
        string workingDirectory,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken,
        IReadOnlyDictionary<string, string?>? environment = null)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        var startInfo = new ProcessStartInfo(fileName)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        if (environment is not null)
        {
            foreach (var variable in environment)
            {
                startInfo.Environment[variable.Key] = variable.Value;
            }
        }

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException($"Process '{fileName}' did not start.");
        var standardOutput = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
        var standardError = process.StandardError.ReadToEndAsync(CancellationToken.None);
        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    using var terminationTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                    await process.WaitForExitAsync(terminationTimeout.Token).ConfigureAwait(false);
                }
            }
            catch (Exception exception) when (exception is InvalidOperationException
                or Win32Exception
                or NotSupportedException
                or OperationCanceledException)
            {
                // Preserve the original cancellation after best-effort child cleanup.
            }

            var outputDrain = Task.WhenAll(standardOutput, standardError);
            try
            {
                await outputDrain.WaitAsync(TimeSpan.FromSeconds(2), CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is TimeoutException
                or IOException
                or InvalidOperationException)
            {
                _ = outputDrain.ContinueWith(
                    static completed => _ = completed.Exception,
                    CancellationToken.None,
                    TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
            }

            throw;
        }

        return new TestProcessResult(
            process.ExitCode,
            await standardOutput.ConfigureAwait(false),
            await standardError.ConfigureAwait(false));
    }
}
