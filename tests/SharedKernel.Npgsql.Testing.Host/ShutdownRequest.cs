using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace PandaBear.Npgsql.Testing.Host;

/// <summary>
/// Tells the host when its owner wants it to stop: standard input reached end-of-file, which is what the owner causes by
/// closing the pipe or by dying, or the process received one of <see cref="TerminationSignals"/>.
/// </summary>
[SuppressMessage("Usage", "CA2213:Disposable fields should be disposed", Justification = "A source without a timer holds no unmanaged resource, and the input watcher may still request a stop after disposal.")]
internal sealed class ShutdownRequest : IDisposable
{
    /// <summary>Gets the signals after which the host stops the server and exits normally, instead of being killed with the container still running.</summary>
    internal static IReadOnlyList<PosixSignal> TerminationSignals { get; } = [PosixSignal.SIGTERM, PosixSignal.SIGINT, PosixSignal.SIGHUP];

    private readonly CancellationTokenSource _requested = new();
    private readonly List<PosixSignalRegistration> _registrations;
    private string? _reason;

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

    /// <summary>Requests a stop for <paramref name="context"/> and keeps the runtime from ending the process by itself.</summary>
    /// <param name="context">The signal that arrived.</param>
    internal void OnSignal(PosixSignalContext context)
    {
        context.Cancel = true;
        Request(context.Signal.ToString());
    }

    /// <inheritdoc />
    public void Dispose() => _registrations.ForEach(static registration => registration.Dispose());

    private void WaitForEndOfFile(Stream standardInput)
    {
        var buffer = new byte[256];
        while (standardInput.Read(buffer) > 0)
        {
            // Input is not a command; only its end is.
        }

        Request("standard input reached end-of-file");
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
