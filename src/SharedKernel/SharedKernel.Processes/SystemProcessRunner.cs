using System.Diagnostics;

namespace PandaBear.Processes;

/// <summary>
/// Runs a real child process exactly as its start information describes, without a time or output limit, so it also
/// suits a long-running server whose standard streams are not redirected. A redirected standard input is closed after
/// the start. On cancellation it kills the whole process tree, waits up to two seconds for it to exit and for its
/// output to drain, then rethrows the cancellation.
/// </summary>
public sealed class SystemProcessRunner : IProcessRunner
{
    /// <inheritdoc />
    public async Task<ProcessOutput> Run(
        ProcessStartInfo startInfo,
        TextWriter? forward,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(startInfo);
        if (!Path.IsPathFullyQualified(startInfo.FileName))
        {
            throw new ArgumentException("The executable must be a fully qualified path.", nameof(startInfo));
        }

        var ct = cancellationToken;
        using var process = new Process { StartInfo = startInfo };
        ct.ThrowIfCancellationRequested();
        if (!process.Start())
        {
            throw new InvalidOperationException($"Could not start {startInfo.FileName}.");
        }

        if (startInfo.RedirectStandardInput)
        {
            process.StandardInput.Close();
        }

        // The two streams are read concurrently, and a TextWriter is not thread-safe, so their lines share one lock.
        Func<string, Task>? writeLine = forward is null ? null : TextWriter.Synchronized(forward).WriteLineAsync;
        var standardOutput = startInfo.RedirectStandardOutput
            ? Read(process.StandardOutput, writeLine)
            : Task.FromResult(string.Empty);
        var standardError = startInfo.RedirectStandardError
            ? Read(process.StandardError, writeLine)
            : Task.FromResult(string.Empty);

        try
        {
            await process.WaitForExitAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Preserve cancellation so the caller can force-remove the named container.
            await ProcessTree.Terminate(process, TimeSpan.FromSeconds(2)).ConfigureAwait(false);

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

        return new ProcessOutput(
            process.ExitCode,
            await standardOutput.ConfigureAwait(false),
            await standardError.ConfigureAwait(false));
    }

    private static async Task<string> Read(StreamReader reader, Func<string, Task>? writeLine)
    {
        if (writeLine is null)
        {
            return await reader.ReadToEndAsync(CancellationToken.None).ConfigureAwait(false);
        }

        while (await reader.ReadLineAsync(CancellationToken.None).ConfigureAwait(false) is { } line)
        {
            await writeLine(line).ConfigureAwait(false);
        }

        return string.Empty;
    }
}
