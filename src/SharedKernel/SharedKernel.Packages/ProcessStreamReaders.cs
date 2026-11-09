using System.Text;

namespace PandaBear.Packages;

/// <summary>
/// Reads the standard output and standard error of a started process, each up to a limit. Disposing waits for both
/// reads, so a scope that owns this object cannot end while a read still uses the process or its token.
/// </summary>
internal sealed class ProcessStreamReaders : IAsyncDisposable
{
    private const int MaxCharacters = 8 * 1024 * 1024;
    private readonly Task<string> _output;
    private readonly Task<string> _error;
    private readonly CancellationTokenSource _overflow;

    private ProcessStreamReaders(Task<string> output, Task<string> error, CancellationTokenSource overflow)
    {
        _output = output;
        _error = error;
        _overflow = overflow;
    }

    /// <summary>Starts reading both streams. Going over the limit cancels <paramref name="overflow"/>, which ends the run.</summary>
    public static ProcessStreamReaders Start(StreamReader output, StreamReader error, CancellationTokenSource overflow, CancellationToken cancellationToken) =>
        new(Read(output, overflow, cancellationToken), Read(error, overflow, cancellationToken), overflow);

    /// <summary>Waits until both streams have ended, or until <paramref name="drainTimeout"/> after the call.</summary>
    /// <exception cref="TimeoutException">A stream stayed open longer than <paramref name="drainTimeout"/>.</exception>
    /// <exception cref="InvalidOperationException">A stream went over the limit, however late: its text is then incomplete.</exception>
    public async Task<(string Output, string Error)> Completed(TimeSpan drainTimeout, CancellationToken cancellationToken)
    {
        await Task.WhenAll(_output, _error).WaitAsync(drainTimeout, cancellationToken).ConfigureAwait(false);

        // Reads that already finished ignore the cancelled token above, and a read over the limit ends with an empty text.
        return _overflow.IsCancellationRequested
            ? throw new InvalidOperationException("The process wrote more output than the limit allows.")
            : (await _output.ConfigureAwait(false), await _error.ConfigureAwait(false));
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        // A read that was cancelled with the run is expected; a finished read has nothing left to report.
        try
        {
            await _output.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Cancelled together with the run.
        }

        try
        {
            await _error.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Cancelled together with the run.
        }
    }

    private static async Task<string> Read(StreamReader reader, CancellationTokenSource overflow, CancellationToken cancellationToken)
    {
        var buffer = new char[8192];
        StringBuilder text = new();
        int read;
        while ((read = await reader.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false)) > 0)
        {
            text.Append(buffer, 0, read);
            if (text.Length > MaxCharacters)
            {
                await overflow.CancelAsync().ConfigureAwait(false);
                return string.Empty;
            }
        }

        return text.ToString();
    }
}
