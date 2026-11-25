using System.Diagnostics.CodeAnalysis;

namespace SharedKernel.IntegrationTesting;

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

    /// <summary>
    /// Runs the probe until it returns a value, pausing <see cref="DefaultInterval"/> between attempts: use it to read state another process
    /// writes later, such as a row, a message or a file.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="probe">The probe; it returns <see langword="null"/> while the state is not there yet, and receives a token cancelled at the deadline.</param>
    /// <param name="timeout">How long to keep trying.</param>
    /// <param name="ct">The caller's token; its cancellation is not reported as a timeout.</param>
    /// <returns>The first value the probe returned.</returns>
    /// <exception cref="TimeoutException">The probe returned no value in time.</exception>
    public static Task<T> Until<T>(Func<CancellationToken, Task<T?>> probe, TimeSpan timeout, CancellationToken ct)
        where T : class =>
        Until(probe, timeout, DefaultInterval, ct);

    /// <summary>Runs the probe until it returns a value.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="probe">The probe; it returns <see langword="null"/> while the state is not there yet, and receives a token cancelled at the deadline.</param>
    /// <param name="timeout">How long to keep trying.</param>
    /// <param name="interval">The pause between two attempts.</param>
    /// <param name="ct">The caller's token; its cancellation is not reported as a timeout.</param>
    /// <returns>The first value the probe returned.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="probe"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeout"/> or <paramref name="interval"/> is not positive.</exception>
    /// <exception cref="TimeoutException">The probe returned no value in time.</exception>
    /// <remarks>
    /// Unlike <see cref="Succeeds(Func{CancellationToken, Task}, TimeSpan, TimeSpan, CancellationToken)"/>, an exception from the probe is not
    /// retried: <see langword="null"/> means "not yet", and a probe that throws is broken, so the exception ends the wait at once.
    /// </remarks>
    public static async Task<T> Until<T>(Func<CancellationToken, Task<T?>> probe, TimeSpan timeout, TimeSpan interval, CancellationToken ct)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(probe);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(interval, TimeSpan.Zero);
        ct.ThrowIfCancellationRequested();

        using var deadline = new CancellationTokenSource(timeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, deadline.Token);
        var attempts = 0;
        try
        {
            while (true)
            {
                attempts++;
                if (await probe(linked.Token).ConfigureAwait(false) is { } value)
                {
                    return value;
                }

                await Task.Delay(interval, linked.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (deadline.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            throw new TimeoutException($"The probe returned no value within {timeout} ({attempts} attempts).");
        }
    }
}
