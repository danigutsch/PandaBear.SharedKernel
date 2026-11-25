using System.Diagnostics.Metrics;

namespace SharedKernel.AspNetCore.Authentication.Bff;

/// <summary>Records ticket store failures using only the operation that failed.</summary>
internal static class TicketStoreMetrics
{
    private static readonly Meter Meter = new(BffTelemetry.Name);
    private static readonly Lazy<Counter<long>> Failures = new(static () => Meter.CreateCounter<long>(
        "sharedkernel.bff.session_ticket.failures",
        "{failure}",
        "Server-side session ticket operations that failed."));

    /// <summary>The closed set of ticket store operations exposed in the counter.</summary>
    internal enum Operation
    {
        /// <summary>Serializes and protects a ticket.</summary>
        Protect,

        /// <summary>Unprotects and deserializes a cached ticket.</summary>
        Unprotect,

        /// <summary>Reads an entry from the cache.</summary>
        CacheGet,

        /// <summary>Writes an entry to the cache.</summary>
        CacheSet,

        /// <summary>Removes an entry from the cache.</summary>
        CacheRemove,
    }

    /// <summary>Determines whether a failure can be counted without interfering with cancellation or fatal errors.</summary>
    /// <param name="exception">The original operation error.</param>
    /// <returns>Whether the error represents a recordable failure.</returns>
    internal static bool CanRecord(Exception exception) =>
        exception is not (OperationCanceledException or OutOfMemoryException or AccessViolationException);

    /// <summary>Increments the counter with the operation as its only dimension.</summary>
    /// <param name="operation">The failed operation.</param>
    /// <exception cref="ArgumentOutOfRangeException">The operation is outside the defined set.</exception>
    internal static void RecordFailure(Operation operation)
    {
        string operationName = operation switch
        {
            Operation.Protect => "protect",
            Operation.Unprotect => "unprotect",
            Operation.CacheGet => "cache_get",
            Operation.CacheSet => "cache_set",
            Operation.CacheRemove => "cache_remove",
            _ => throw new ArgumentOutOfRangeException(nameof(operation)),
        };

        try
        {
            Failures.Value.Add(1, new KeyValuePair<string, object?>("sharedkernel.bff.session_ticket.operation", operationName));
        }
        catch (Exception exception) when (exception is not (OutOfMemoryException or AccessViolationException))
        {
            // Metrics observers must not alter the authentication result or replace the original error.
        }
    }
}
