using System.Diagnostics.CodeAnalysis;
using System.Runtime.ExceptionServices;

namespace PandaBear.Npgsql.Testing;

/// <summary>Runs test cleanup actions so that every action is attempted and no failure is lost.</summary>
internal static class PostgreSqlTestCleanup
{
    /// <summary>Runs every cleanup action in order after an optional operation failure, then rethrows a single failure as is or several wrapped in an AggregateException with the operation failure first.</summary>
    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Cleanup must attempt every action and report all failures without losing the operation failure.")]
    internal static async Task Run(
        Exception? operationFailure,
        params Func<ValueTask>[] cleanupActions)
    {
        List<Exception> failures = operationFailure is null ? [] : [operationFailure];
        foreach (var cleanupAction in cleanupActions)
        {
            try
            {
                await cleanupAction();
            }
            catch (Exception cleanupFailure)
            {
                failures.Add(cleanupFailure);
            }
        }

        if (failures.Count == 0)
        {
            return;
        }

        if (failures.Count == 1)
        {
            ExceptionDispatchInfo.Capture(failures[0]).Throw();
        }

        throw new AggregateException("PostgreSQL test cleanup failed.", failures);
    }
}
