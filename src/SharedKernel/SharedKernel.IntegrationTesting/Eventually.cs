using System.Diagnostics.CodeAnalysis;

namespace PandaBear.IntegrationTesting;

/// <summary>
/// Retries an assertion until it passes or a timeout ends it, for state another process reaches later (a cache that refreshes, a redirect
/// written by another host). Use it instead of fixed sleeps.
/// </summary>
public static class Eventually
{
    /// <summary>The default pause between two attempts.</summary>
    public static readonly TimeSpan DefaultInterval = TimeSpan.FromMilliseconds(250);

    /// <summary>Runs the assertion until it does not throw, pausing <see cref="DefaultInterval"/> between attempts.</summary>
    /// <param name="assertion">The assertion; it receives a token cancelled at the deadline.</param>
    /// <param name="timeout">How long to keep trying.</param>
    /// <param name="ct">The caller's token; its cancellation is not reported as a timeout.</param>
    /// <returns>A task that completes when the assertion passed.</returns>
    /// <exception cref="TimeoutException">The assertion did not pass in time; the last failure is the inner exception.</exception>
    public static Task Succeeds(Func<CancellationToken, Task> assertion, TimeSpan timeout, CancellationToken ct) =>
        Succeeds(assertion, timeout, DefaultInterval, ct);

    /// <summary>Runs the assertion until it does not throw.</summary>
    /// <param name="assertion">The assertion; it receives a token cancelled at the deadline.</param>
    /// <param name="timeout">How long to keep trying.</param>
    /// <param name="interval">The pause between two attempts.</param>
    /// <param name="ct">The caller's token; its cancellation is not reported as a timeout.</param>
    /// <returns>A task that completes when the assertion passed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="assertion"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeout"/> or <paramref name="interval"/> is not positive.</exception>
    /// <exception cref="TimeoutException">The assertion did not pass in time; the last failure is the inner exception.</exception>
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Any failure of an attempt is retried and the last one is reported.")]
    public static async Task Succeeds(Func<CancellationToken, Task> assertion, TimeSpan timeout, TimeSpan interval, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(assertion);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(interval, TimeSpan.Zero);
        ct.ThrowIfCancellationRequested();

        using var deadline = new CancellationTokenSource(timeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, deadline.Token);
        Exception? last = null;
        var attempts = 0;
        while (!linked.IsCancellationRequested)
        {
            attempts++;
            try
            {
                await assertion(linked.Token).ConfigureAwait(false);
                return;
            }
            catch (Exception failure) when (!ct.IsCancellationRequested)
            {
                // A cancellation caused by the deadline is the timeout, not a failure of the assertion.
                if (!(failure is OperationCanceledException && deadline.IsCancellationRequested))
                {
                    last = failure;
                }
            }

            try
            {
                await Task.Delay(interval, linked.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                break;
            }
        }

        ct.ThrowIfCancellationRequested();
        throw new TimeoutException($"The condition did not hold within {timeout} ({attempts} attempts).", last);
    }
}
