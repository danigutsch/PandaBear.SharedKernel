using System.Security.Cryptography;

namespace SharedKernel.Secrets;

/// <summary>
/// Feeds a started process its standard input and reads its standard output and error, each into a bounded buffer.
/// Disposing waits for all three, so a scope that owns this object cannot end while a stream still uses the process or its token.
/// </summary>
internal sealed class ToolStreams : IAsyncDisposable
{
    private readonly Task _input;
    private readonly Task _output;
    private readonly Task _error;

    private ToolStreams(Task input, Task output, Task error)
    {
        _input = input;
        _output = output;
        _error = error;
    }

    /// <summary>Starts the three transfers. Output over its limit cancels <paramref name="overflow"/>.</summary>
    internal static ToolStreams Start(
        Stream input,
        ReadOnlyMemory<byte> bytes,
        Stream output,
        ToolOutputBuffer outputBuffer,
        Stream error,
        ToolOutputBuffer errorBuffer,
        CancellationTokenSource overflow,
        CancellationToken cancellationToken) =>
        new(Write(input, bytes, cancellationToken), Read(output, outputBuffer, overflow, cancellationToken), Read(error, errorBuffer, overflow, cancellationToken));

    /// <summary>Waits for all three transfers, at most <paramref name="drainTimeout"/>.</summary>
    /// <exception cref="TimeoutException">A stream stayed open.</exception>
    internal Task Completed(TimeSpan drainTimeout, CancellationToken cancellationToken) =>
        Task.WhenAll(_input, _output, _error).WaitAsync(drainTimeout, cancellationToken);

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        foreach (var task in new[] { _input, _output, _error })
        {
            try
            {
                await task.ConfigureAwait(false);
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

    private static async Task Read(Stream stream, ToolOutputBuffer buffer, CancellationTokenSource overflow, CancellationToken cancellationToken)
    {
        var chunk = new byte[8192];
        try
        {
            int read;
            while ((read = await stream.ReadAsync(chunk, cancellationToken).ConfigureAwait(false)) > 0)
            {
                buffer.Append(chunk.AsSpan(0, read));
                if (buffer.Overflowed)
                {
                    await overflow.CancelAsync().ConfigureAwait(false);
                    return;
                }
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(chunk);
        }
    }
}
