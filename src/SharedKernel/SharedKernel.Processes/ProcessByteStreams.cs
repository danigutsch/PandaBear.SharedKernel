using System.Runtime.ExceptionServices;

namespace SharedKernel.Processes;

/// <summary>
/// Transfers the byte streams of a running process. An output overrun or transfer failure signals cancellation
/// immediately, including while the child waits with a full pipe. Disposing waits for all transfers to end.
/// </summary>
internal sealed class ProcessByteStreams : IAsyncDisposable
{
    private readonly Func<Task> _cancelExecution;
    private readonly Task _transfers;
    private ExceptionDispatchInfo? _failure;

    /// <summary>Starts transferring both output pipes and optionally writing and closing standard input.</summary>
    public ProcessByteStreams(
        Stream? input,
        ReadOnlyMemory<byte> bytes,
        Stream standardOutput,
        Stream output,
        long maxOutputBytes,
        Stream standardError,
        Stream error,
        long maxErrorBytes,
        Func<Task> cancelExecution,
        CancellationToken cancellationToken)
    {
        _cancelExecution = cancelExecution;
        _transfers = Task.WhenAll(
            input is null ? Task.CompletedTask : WriteInput(input, bytes, cancellationToken),
            Copy(standardOutput, output, maxOutputBytes, cancellationToken),
            Copy(standardError, error, maxErrorBytes, cancellationToken));
    }

    /// <summary>Waits until the transfers have ended, up to the drain timeout.</summary>
    public Task Completed(TimeSpan drainTimeout, CancellationToken cancellationToken) =>
        _transfers.WaitAsync(drainTimeout, cancellationToken);

    /// <summary>Throws the first truncation or transfer failure with its original stack.</summary>
    public void ThrowIfFailed() => Volatile.Read(ref _failure)?.Throw();

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _transfers.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
    }

    private async Task WriteInput(Stream stream, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
    {
        try
        {
            await using (stream.ConfigureAwait(false))
            {
                if (!bytes.IsEmpty)
                {
                    await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
                }
            }
        }
        catch (IOException)
        {
            // The child exited without reading all its input; its exit code reports what happened.
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            await Fail(exception).ConfigureAwait(false);
        }
    }

    private async Task Copy(Stream source, Stream destination, long remaining, CancellationToken cancellationToken)
    {
        try
        {
            var buffer = new byte[8192];
            int read;
            while ((read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
            {
                var count = (int)Math.Min(read, remaining);
                if (count > 0)
                {
                    await destination.WriteAsync(buffer.AsMemory(0, count), cancellationToken).ConfigureAwait(false);
                    remaining -= count;
                }
                if (read > count)
                {
                    throw new InvalidOperationException("The process wrote more output than the limit allows.");
                }
            }
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            await Fail(exception).ConfigureAwait(false);
        }
    }

    private async Task Fail(Exception exception)
    {
        if (Interlocked.CompareExchange(ref _failure, ExceptionDispatchInfo.Capture(exception), null) is null)
        {
            await _cancelExecution().ConfigureAwait(false);
        }
    }
}
