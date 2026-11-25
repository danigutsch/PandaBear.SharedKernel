using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace SharedKernel.Processes;

/// <summary>
/// Runs a real child process that is expected to finish quickly, such as a package manager query, with a time limit (two
/// minutes by default) and a limit on the standard output and on the standard error it keeps. Output above a limit ends
/// the process. It redirects and captures both output streams and closes a redirected standard input, so the start
/// information it receives is updated to redirect them; the caller sets the arguments, the working directory, and the
/// environment.
/// </summary>
public sealed class BoundedProcessRunner : IProcessRunner
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan DrainTimeout = TimeSpan.FromSeconds(5);

    private readonly TimeSpan _timeout;
    private readonly string _timeoutMessage;

    /// <summary>Creates a runner with a two-minute time limit.</summary>
    public BoundedProcessRunner()
        : this(DefaultTimeout)
    {
    }

    /// <summary>Creates a runner with the given time limit.</summary>
    /// <param name="timeout">The longest a process may run; it must be positive.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeout"/> is not positive.</exception>
    public BoundedProcessRunner(TimeSpan timeout)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);
        _timeout = timeout;
        _timeoutMessage = timeout == DefaultTimeout
            ? "The process did not finish within two minutes."
            : string.Create(CultureInfo.InvariantCulture, $"The process did not finish within {timeout.TotalSeconds} seconds.");
    }

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">
    /// The process cannot be started, writes more than the output limit, or leaves its output open after it exits. A start
    /// failure has the message <c>Failed to start &lt;file&gt;: &lt;reason&gt;</c> with the operating system's reason, which the
    /// inner <see cref="Win32Exception"/> also carries, as <see cref="ProcessTree.Start"/> reports it.
    /// </exception>
    /// <exception cref="TimeoutException">
    /// The process runs longer than the time limit: <c>The process did not finish within two minutes.</c> by default, or the
    /// limit in seconds.
    /// </exception>
    public async Task<ProcessOutput> Run(ProcessStartInfo startInfo, TextWriter? forward, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(startInfo);
        if (!Path.IsPathFullyQualified(startInfo.FileName))
        {
            throw new ArgumentException("The executable must be an absolute path.", nameof(startInfo));
        }

        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;
        startInfo.UseShellExecute = false;

        using var timeout = new CancellationTokenSource(_timeout);
        using var overflow = new CancellationTokenSource();
        using var execution = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token, overflow.Token);
        using var process = Start(startInfo);

        if (startInfo.RedirectStandardInput)
        {
            process.StandardInput.Close();
        }

        // Declared after the process and the token sources, so it is disposed first and waits for the reads.
        var readers = ProcessStreamReaders.Start(process.StandardOutput, process.StandardError, forward, overflow, execution.Token);
        await using var readersScope = readers.ConfigureAwait(false);
        var finished = false;
        try
        {
            await process.WaitForExitAsync(execution.Token).ConfigureAwait(false);

            // A descendant that inherited a pipe can keep it open after the process exited; do not wait for it forever.
            (string Output, string Error) text;
            try
            {
                text = await readers.Completed(DrainTimeout, execution.Token).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                throw new InvalidOperationException("The process left its output open after it exited.");
            }

            finished = true;
            return new ProcessOutput(process.ExitCode, text.Output, text.Error);
        }
        catch (OperationCanceledException)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }

            if (overflow.IsCancellationRequested)
            {
                throw new InvalidOperationException("The process wrote more output than the limit allows.");
            }

            throw new TimeoutException(_timeoutMessage);
        }
        finally
        {
            if (!finished)
            {
                // Stop the process tree and cancel the reads, so disposing the readers cannot wait on an open pipe, then wait
                // briefly for the tree to exit. A descendant that already left the tree and holds a pipe is not stopped.
                Stop(process);
                await execution.CancelAsync().ConfigureAwait(false);
                await WaitForExit(process).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Runs the process that <paramref name="startInfo"/> describes, writes <paramref name="standardInput"/> to it and
    /// closes its standard input, and returns the exact bytes it writes to standard output, such as NUL-separated paths.
    /// It redirects all three standard streams. Above either output limit, after the time limit, or on cancellation the
    /// process tree is ended.
    /// </summary>
    /// <param name="startInfo">The process to run. Its <see cref="ProcessStartInfo.FileName"/> is a fully qualified path.</param>
    /// <param name="standardInput">The bytes written to standard input before it is closed; empty closes it at once.</param>
    /// <param name="maxOutputBytes">The most standard output bytes the process may write.</param>
    /// <param name="maxErrorBytes">The most standard error bytes the process may write.</param>
    /// <param name="cancellationToken">Cancels the run and ends the process tree.</param>
    /// <returns>The exit code, the standard output bytes, and the standard error text.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="startInfo"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The file name of <paramref name="startInfo"/> is not a fully qualified path.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A limit is not positive.</exception>
    /// <exception cref="InvalidOperationException">
    /// The process cannot be started (<c>Failed to start &lt;file&gt;: &lt;reason&gt;</c>, with the operating system's
    /// <see cref="Win32Exception"/> inside), writes more than a limit, or leaves its output open after it exits.
    /// </exception>
    /// <exception cref="TimeoutException">The process runs longer than the time limit.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled; the exception carries it.</exception>
    public async Task<CapturedProcessOutput> Capture(
        ProcessStartInfo startInfo,
        ReadOnlyMemory<byte> standardInput,
        int maxOutputBytes,
        int maxErrorBytes,
        CancellationToken cancellationToken)
    {
        using MemoryStream output = new();
        using MemoryStream error = new();
        var exitCode = await RunBytes(startInfo, standardInput, output, maxOutputBytes, error, maxErrorBytes, cancellationToken).ConfigureAwait(false);
        return new CapturedProcessOutput(exitCode, output.ToArray(), Encoding.UTF8.GetString(error.GetBuffer(), 0, (int)error.Length));
    }

    /// <summary>
    /// Runs a direct child process and copies its output to two caller-owned streams. Both streams are drained
    /// concurrently. A transfer failure, output above a limit, a timeout, or cancellation ends the process tree.
    /// </summary>
    /// <remarks>
    /// The destinations must honor cancellation during asynchronous writes. The runner waits for any in-flight
    /// writes before it returns so that the caller can safely reuse or dispose each destination.
    /// </remarks>
    /// <param name="startInfo">The process to run. Its <see cref="ProcessStartInfo.FileName"/> is a fully qualified path.</param>
    /// <param name="output">A writable stream for standard output that remains open after the run.</param>
    /// <param name="maxOutputBytes">The most standard output bytes to write, including on failure.</param>
    /// <param name="error">A separate writable stream for standard error that remains open after the run.</param>
    /// <param name="maxErrorBytes">The most standard error bytes to write, including on failure.</param>
    /// <param name="cancellationToken">Cancels the run and ends the process tree.</param>
    /// <returns>The child process exit code.</returns>
    /// <exception cref="ArgumentNullException">A required argument is null.</exception>
    /// <exception cref="ArgumentException">The executable is relative or the output streams are not separate and writable.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A limit is not positive.</exception>
    /// <exception cref="InvalidOperationException">The process could not start, exceeded a limit, or left a pipe open.</exception>
    /// <exception cref="IOException">A stream transfer failed.</exception>
    /// <exception cref="TimeoutException">The process runs longer than the time limit.</exception>
    /// <exception cref="OperationCanceledException">The caller canceled the run.</exception>
    public Task<int> RunToStreams(
        ProcessStartInfo startInfo,
        Stream output,
        long maxOutputBytes,
        Stream error,
        long maxErrorBytes,
        CancellationToken cancellationToken) =>
        RunBytes(startInfo, null, output, maxOutputBytes, error, maxErrorBytes, cancellationToken);

    private async Task<int> RunBytes(
        ProcessStartInfo startInfo,
        ReadOnlyMemory<byte>? standardInput,
        Stream output,
        long maxOutputBytes,
        Stream error,
        long maxErrorBytes,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(startInfo);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);
        if (!Path.IsPathFullyQualified(startInfo.FileName))
        {
            throw new ArgumentException("The executable must be an absolute path.", nameof(startInfo));
        }

        if (!output.CanWrite || !error.CanWrite || ReferenceEquals(output, error))
        {
            throw new ArgumentException("Standard output and standard error require separate writable streams.");
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxOutputBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxErrorBytes);
        cancellationToken.ThrowIfCancellationRequested();

        startInfo.RedirectStandardInput |= standardInput.HasValue;
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;
        startInfo.UseShellExecute = false;

        using var timeout = new CancellationTokenSource(_timeout);
        using var failureSignal = new CancellationTokenSource();
        using var execution = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token, failureSignal.Token);
        using var process = Start(startInfo);
        var streams = new ProcessByteStreams(
            startInfo.RedirectStandardInput ? process.StandardInput.BaseStream : null,
            standardInput.GetValueOrDefault(),
            process.StandardOutput.BaseStream,
            output,
            maxOutputBytes,
            process.StandardError.BaseStream,
            error,
            maxErrorBytes,
            failureSignal.CancelAsync,
            execution.Token);
        await using var streamsScope = streams.ConfigureAwait(false);
        var finished = false;
        try
        {
            await process.WaitForExitAsync(execution.Token).ConfigureAwait(false);
            try
            {
                await streams.Completed(DrainTimeout, execution.Token).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                throw new InvalidOperationException("The process left its output open after it exited.");
            }

            streams.ThrowIfFailed();
            finished = true;
            return process.ExitCode;
        }
        catch (OperationCanceledException exception)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException(exception.Message, exception, cancellationToken);
            }

            streams.ThrowIfFailed();
            throw new TimeoutException(_timeoutMessage);
        }
        finally
        {
            if (!finished)
            {
                Stop(process);
                await execution.CancelAsync().ConfigureAwait(false);
                await WaitForExit(process).ConfigureAwait(false);
            }
        }
    }

    private static Process Start(ProcessStartInfo startInfo)
    {
        var process = new Process { StartInfo = startInfo };
        try
        {
            if (!process.Start())
            {
                throw new InvalidOperationException($"Failed to start {startInfo.FileName}.");
            }

            return process;
        }
        catch (Win32Exception exception)
        {
            process.Dispose();

            // Reported as ProcessTree.Start reports it, so every start failure is the documented InvalidOperationException.
            throw new InvalidOperationException($"Failed to start {startInfo.FileName}: {exception.Message}", exception);
        }
        catch
        {
            process.Dispose();
            throw;
        }
    }

    private static void Stop(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException or Win32Exception)
        {
            // The process ended between the check and the kill.
        }
    }

    private static async Task WaitForExit(Process process)
    {
        using var limit = new CancellationTokenSource(DrainTimeout);
        try
        {
            await process.WaitForExitAsync(limit.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // The process did not exit within the limit; nothing more can be done here.
        }
    }
}
