using System.Diagnostics.CodeAnalysis;
using System.Runtime.ExceptionServices;

namespace SharedKernel.EventSourcing.Npgsql.Tests;

internal static class PostgreSqlTestCleanup
{
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
