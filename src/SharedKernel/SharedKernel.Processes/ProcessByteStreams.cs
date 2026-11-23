namespace PandaBear.Processes;

/// <summary>
/// Writes the standard input of a started process and reads its standard output and standard error as bytes, each up to
/// a limit. Disposing waits for all three, so a scope that owns this object cannot end while a stream still uses the
/// process or its token.
/// </summary>
internal sealed class ProcessByteStreams : IAsyncDisposable
{
    private readonly Task _input;
    private readonly Task<byte[]> _output;
    private readonly Task<byte[]> _error;
    private readonly CancellationTokenSource _overflow;

    private ProcessByteStreams(Task input, Task<byte[]> output, Task<byte[]> error, CancellationTokenSource overflow)
    {
        _input = input;
        _output = output;
        _error = error;
        _overflow = overflow;
    }

    /// <summary>
    /// Starts the three transfers. Writing <paramref name="bytes"/> closes standard input; output over its limit cancels
    /// <paramref name="overflow"/>, which ends the run.
    /// </summary>
    public static ProcessByteStreams Start(
        Stream input,
        ReadOnlyMemory<byte> bytes,
        Stream output,
        int maxOutputBytes,
        Stream error,
        int maxErrorBytes,
        CancellationTokenSource overflow,
        CancellationToken cancellationToken) =>
        new(
            Write(input, bytes, cancellationToken),
            Read(output, maxOutputBytes, overflow, cancellationToken),
            Read(error, maxErrorBytes, overflow, cancellationToken),
            overflow);

    /// <summary>Waits until all three transfers have ended, or until <paramref name="drainTimeout"/> after the call.</summary>
    /// <exception cref="TimeoutException">A stream stayed open longer than <paramref name="drainTimeout"/>.</exception>
    /// <exception cref="InvalidOperationException">A stream went over its limit, however late: its bytes are then incomplete.</exception>
    public async Task<(byte[] Output, byte[] Error)> Completed(TimeSpan drainTimeout, CancellationToken cancellationToken)
    {
        await Task.WhenAll(_input, _output, _error).WaitAsync(drainTimeout, cancellationToken).ConfigureAwait(false);
        return _overflow.IsCancellationRequested
            ? throw new InvalidOperationException("The process wrote more output than the limit allows.")
            : (await _output.ConfigureAwait(false), await _error.ConfigureAwait(false));
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        foreach (var transfer in new Task[] { _input, _output, _error })
        {
            try
            {
                await transfer.ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is OperationCanceledException or IOException or ObjectDisposedException)
            {
                // Cancelled with the run, or the pipe closed.
            }
        }
    }

    private static async Task Write(Stream stream, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
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
    }

    private static async Task<byte[]> Read(Stream stream, int limit, CancellationTokenSource overflow, CancellationToken cancellationToken)
    {
        using MemoryStream content = new();
        var buffer = new byte[8192];
        int read;
        while ((read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            if (read > limit - content.Length)
            {
                await overflow.CancelAsync().ConfigureAwait(false);
                return [];
            }

            await content.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
        }

        return content.ToArray();
    }
}
