using System.Diagnostics.CodeAnalysis;
using System.Runtime.ExceptionServices;

namespace SharedKernel.EntityFrameworkCore.Tests;

internal static class PostgreSqlScenarioCleanup
{
    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Cleanup must attempt every resource and report all failures without losing the operation failure.")]
    internal static async Task DisposeResources(
        Exception? operationFailure,
        params IAsyncDisposable?[] resources)
    {
        List<Exception> failures = operationFailure is null ? [] : [operationFailure];
        foreach (var resource in resources)
        {
            if (resource is null)
            {
                continue;
            }

            try
            {
                await resource.DisposeAsync();
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

        throw new AggregateException("PostgreSQL scenario cleanup failed.", failures);
    }
}
