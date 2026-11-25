using System.Diagnostics.CodeAnalysis;
using System.Runtime.ExceptionServices;

namespace SharedKernel.Testing;

/// <summary>
/// Releases test resources so that every one is attempted and no failure is lost, typically in a fixture's dispose or in the
/// <see langword="catch"/> of a failed set-up. Nothing is thrown when everything succeeds; a single failure is rethrown as it is, with its
/// stack trace, so a caller can still match its type; several are thrown as one <see cref="AggregateException"/> with the operation failure
/// first and the cleanup failures in order.
/// </summary>
public static class TestCleanup
{
    /// <summary>Disposes every resource that is not <see langword="null"/>, in order, then reports the failures.</summary>
    /// <param name="operationFailure">The failure of the operation being cleaned up after, such as a set-up, or <see langword="null"/>.</param>
    /// <param name="resources">The resources; <see langword="null"/> entries, for resources never created, are skipped.</param>
    /// <returns>A task that completes when every resource was disposed and nothing failed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="resources"/> is <see langword="null"/>.</exception>
    /// <exception cref="AggregateException">More than one failure, including <paramref name="operationFailure"/>.</exception>
    public static Task DisposeAll(Exception? operationFailure, params IAsyncDisposable?[] resources)
    {
        ArgumentNullException.ThrowIfNull(resources);

        return Run(operationFailure, [.. resources.OfType<IAsyncDisposable>().Select(static resource => (Func<ValueTask>)resource.DisposeAsync)]);
    }

    /// <summary>Runs every cleanup action, in order, then reports the failures. An action that throws counts as one that failed.</summary>
    /// <param name="operationFailure">The failure of the operation being cleaned up after, such as a set-up, or <see langword="null"/>.</param>
    /// <param name="cleanupActions">The actions.</param>
    /// <returns>A task that completes when every action ran and nothing failed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="cleanupActions"/> or one of its entries is <see langword="null"/>.</exception>
    /// <exception cref="AggregateException">More than one failure, including <paramref name="operationFailure"/>.</exception>
    public static Task Run(Exception? operationFailure, params Func<ValueTask>[] cleanupActions)
    {
        ArgumentNullException.ThrowIfNull(cleanupActions);
        foreach (var action in cleanupActions)
        {
            ArgumentNullException.ThrowIfNull(action, nameof(cleanupActions));
        }

        return RunCore(operationFailure, cleanupActions);
    }

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Cleanup must attempt every action and report all failures without losing the operation failure.")]
    private static async Task RunCore(Exception? operationFailure, Func<ValueTask>[] cleanupActions)
    {
        List<Exception> failures = operationFailure is null ? [] : [operationFailure];
        foreach (var action in cleanupActions)
        {
            try
            {
                await action().ConfigureAwait(false);
            }
            catch (Exception cleanupFailure)
            {
                failures.Add(cleanupFailure);
            }
        }

        if (failures.Count == 1)
        {
            ExceptionDispatchInfo.Capture(failures[0]).Throw();
        }

        if (failures.Count > 1)
        {
            throw new AggregateException("Test cleanup failed.", failures);
        }
    }
}
