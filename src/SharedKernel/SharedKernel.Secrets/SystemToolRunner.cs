using System.Diagnostics;
using System.Text;
using SharedKernel.Processes;

namespace SharedKernel.Secrets;

/// <summary>
/// Runs a real child process from an absolute path without a shell. The child inherits <b>no</b> environment: it gets
/// <c>LC_ALL=C</c> and the variables of the request, except a forge token (<see cref="ProcessEnvironment.ForgeTokenVariableNames"/>), which never reaches it. Standard input is written from the request and closed, standard output
/// is limited to 16 MiB (more ends the process), standard error is limited to 64 KiB (more ends the process too), and the run has a time limit
/// (two minutes by default).
/// </summary>
/// <remarks>
/// The output bytes are collected in buffers that are overwritten when they are replaced, then moved into a pinned
/// <see cref="SecretBytes"/>. The process's own memory, pipes, and the operating system's buffers are outside this library's control.
/// </remarks>
public sealed class SystemToolRunner : IToolRunner
{
    private const int MaxOutputBytes = 16 * 1024 * 1024;
    private const int MaxErrorBytes = 64 * 1024;
    private static readonly TimeSpan DrainTimeout = TimeSpan.FromSeconds(5);

    private readonly TimeSpan _timeout;

    /// <summary>Creates a runner with a two-minute time limit.</summary>
    public SystemToolRunner()
        : this(TimeSpan.FromMinutes(2))
    {
    }

    /// <summary>Creates a runner.</summary>
    /// <param name="timeout">The longest a process may run; it must be positive.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeout"/> is not positive.</exception>
    public SystemToolRunner(TimeSpan timeout)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);
        _timeout = timeout;
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">The process cannot be started, writes more than the output limit, or leaves its output open after it exits.</exception>
    /// <exception cref="TimeoutException">The process runs longer than the time limit.</exception>
    public async Task<ToolResult> Run(ToolRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ProcessStartInfo startInfo = new(request.Executable)
        {
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        if (request.WorkingDirectory is not null)
        {
            startInfo.WorkingDirectory = request.WorkingDirectory;
        }

        startInfo.Environment.Clear();
        foreach (var argument in request.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        foreach (var (name, value) in request.Environment)
        {
            startInfo.Environment[name] = value;
        }

        startInfo.Environment["LC_ALL"] = "C";
        ProcessEnvironment.RemoveForgeTokens(startInfo);

        using CancellationTokenSource timeout = new(_timeout);
        using CancellationTokenSource overflow = new();
        using var execution = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token, overflow.Token);
        using var process = ProcessTree.Start(startInfo);

        ToolOutputBuffer output = new(MaxOutputBytes);
        ToolOutputBuffer error = new(MaxErrorBytes);

        // Declared after the process and the token sources, so it is disposed first and waits for the transfers.
        var streams = ToolStreams.Start(
            process.StandardInput.BaseStream,
            request.StandardInput,
            process.StandardOutput.BaseStream,
            output,
            process.StandardError.BaseStream,
            error,
            overflow,
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

            if (output.Overflowed)
            {
                throw new InvalidOperationException("The process wrote more output than the limit allows.");
            }

            finished = true;
            using var errorBytes = error.Take();
            return new ToolResult(process.ExitCode, output.Take(), Encoding.UTF8.GetString(errorBytes.AsSpan()));
        }
        catch (OperationCanceledException)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }

            throw overflow.IsCancellationRequested
                ? new InvalidOperationException("The process wrote more output than the limit allows.")
                : new TimeoutException("The process did not finish within its time limit.");
        }
        finally
        {
            if (!finished)
            {
                // Stop the process tree and cancel the transfers so that disposing the streams cannot wait on an open pipe.
                await ProcessTree.Terminate(process, DrainTimeout).ConfigureAwait(false);
                await execution.CancelAsync().ConfigureAwait(false);
                output.Clear();
            }
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// The child gets the variables of the request and nothing else, except a forge token, which never reaches it; unlike
    /// <see cref="Run"/>, <c>LC_ALL</c> is not set, so an editor shows text in the locale the request gives it.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The request has standard input.</exception>
    /// <exception cref="InvalidOperationException">The process cannot be started.</exception>
    public async Task<int> RunAttached(ToolRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!request.StandardInput.IsEmpty)
        {
            throw new ArgumentException("An attached process reads the caller's standard input; the request cannot carry any.", nameof(request));
        }

        ProcessStartInfo startInfo = new(request.Executable) { UseShellExecute = false };
        if (request.WorkingDirectory is not null)
        {
            startInfo.WorkingDirectory = request.WorkingDirectory;
        }

        startInfo.Environment.Clear();
        foreach (var argument in request.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        foreach (var (name, value) in request.Environment)
        {
            startInfo.Environment[name] = value;
        }

        ProcessEnvironment.RemoveForgeTokens(startInfo);
        using var process = ProcessTree.Start(startInfo);
        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            await ProcessTree.Terminate(process, DrainTimeout).ConfigureAwait(false);
            throw;
        }

        return process.ExitCode;
    }
}
