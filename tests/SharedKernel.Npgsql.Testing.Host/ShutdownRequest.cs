using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace PandaBear.Npgsql.Testing.Host;

/// <summary>
/// Tells the host when its owner wants it to stop: standard input reached end-of-file, which is what the owner causes by
/// closing the pipe or by dying, or failed because the terminal behind it hung up, or the process received one of
/// <see cref="TerminationSignals"/>. The first signal starts a graceful stop that the runtime does not cut short. A second
/// signal ends the process at once, because that stop is stuck or too slow; DCP removes the container of a host that it
/// monitored.
/// </summary>
[SuppressMessage("Usage", "CA2213:Disposable fields should be disposed", Justification = "A source without a timer holds no unmanaged resource, and the input watcher may still request a stop after disposal.")]
internal sealed class ShutdownRequest : IDisposable
{
    /// <summary>Gets the signals after which the host stops the server and exits normally, instead of being killed with the container still running.</summary>
    internal static IReadOnlyList<PosixSignal> TerminationSignals { get; } = [PosixSignal.SIGTERM, PosixSignal.SIGINT, PosixSignal.SIGHUP];

    private readonly CancellationTokenSource _requested = new();
    private readonly List<PosixSignalRegistration> _registrations;
    private string? _reason;
    private int _signals;

    /// <summary>Starts watching <paramref name="standardInput"/> and <paramref name="signals"/>.</summary>
    /// <param name="standardInput">Read to its end; whatever it delivers before that is ignored.</param>
    /// <param name="signals">The signals that request a stop; the host passes <see cref="TerminationSignals"/>.</param>
    internal ShutdownRequest(Stream standardInput, IEnumerable<PosixSignal> signals)
    {
        _registrations = [.. signals.Select(signal => PosixSignalRegistration.Create(signal, OnSignal))];

        // A read of standard input cannot be cancelled, so a background thread waits for the end without keeping the process alive.
        new Thread(() => WaitForEndOfFile(standardInput)) { IsBackground = true }.Start();
    }

    /// <summary>Gets a token that is cancelled when a stop is requested.</summary>
    internal CancellationToken Token => _requested.Token;

    /// <summary>Gets what requested the stop first, or <see langword="null"/> while none did.</summary>
    internal string? Reason => Volatile.Read(ref _reason);

    /// <summary>Completes when a stop is requested.</summary>
    /// <returns>A task that never faults.</returns>
    internal async Task Wait()
    {
        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // The stop that this method waits for.
        }
    }

    /// <summary>
    /// Requests a stop for <paramref name="context"/>. The first signal keeps the runtime from ending the process by
    /// itself, so the host can stop the server first; a later signal leaves the default action, which ends the process.
    /// </summary>
    /// <param name="context">The signal that arrived.</param>
    internal void OnSignal(PosixSignalContext context)
    {
        // Only signals count: a hangup also fails standard input, and the owner closing it is no impatient second request.
        context.Cancel = Interlocked.Increment(ref _signals) == 1;
        Request(context.Signal.ToString());
    }

    /// <inheritdoc />
    public void Dispose() => _registrations.ForEach(static registration => registration.Dispose());

    private void WaitForEndOfFile(Stream standardInput)
    {
        var buffer = new byte[256];
        try
        {
            while (standardInput.Read(buffer) > 0)
            {
                // Input is not a command; only its end is.
            }

            Request("standard input reached end-of-file");
        }
        catch (Exception failure) when (failure is IOException or ObjectDisposedException)
        {
            // A terminal that hangs up fails the read (EIO) instead of ending it. An exception on this thread would end the
            // process before the server stopped, so the failure is a stop request like the hangup signal that follows it.
            Request("standard input could not be read");
        }
    }

    private void Request(string reason)
    {
        if (Interlocked.CompareExchange(ref _reason, reason, null) is null)
        {
            // The callbacks of the token run on the thread pool, not on the signal or input thread that got here.
            _ = _requested.CancelAsync();
        }
    }
}
