using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace PandaBear.Blazor;

/// <summary>
/// Logs handled UI failures with only the exception type name, the operation name, and the trace id, then counts them.
/// </summary>
/// <remarks>
/// The exception object, its message, its stack, and its data are never logged. A counter that throws cannot break
/// reporting: a fixed warning (event id <see cref="CounterFailureEventId" />, no exception text) is logged instead. Operation names that are blank,
/// longer than <see cref="MaxOperationLength" />, or contain characters other than letters, digits, and
/// <c>. _ - : /</c> are replaced by <see cref="UnknownOperation" />.
/// </remarks>
public sealed class UiFailureReporter : IUiFailureReporter
{
    /// <summary>The stable <see cref="EventId" /> number of the handled-failure log entry.</summary>
    public const int HandledFailureEventId = 7001;

    /// <summary>The stable event id number of the warning logged when the counter throws.</summary>
    public const int CounterFailureEventId = 7002;

    /// <summary>The longest operation name that is logged unchanged.</summary>
    public const int MaxOperationLength = 100;

    /// <summary>The operation logged when the supplied name is not safe.</summary>
    public const string UnknownOperation = "unknown";

    /// <summary>The trace id logged when no <see cref="Activity" /> is current.</summary>
    public const string UnknownTraceId = "unknown";

    private readonly ILogger<UiFailureReporter> _logger;
    private readonly IHandledFailureCounter? _counter;

    /// <summary>Initializes a reporter.</summary>
    /// <param name="logger">The logger.</param>
    /// <param name="counter">The optional handled-failure counter.</param>
    public UiFailureReporter(ILogger<UiFailureReporter> logger, IHandledFailureCounter? counter = null)
    {
        ArgumentNullException.ThrowIfNull(logger);

        _logger = logger;
        _counter = counter;
    }

    /// <inheritdoc />
    public void Report(Exception exception, string operation)
    {
        ArgumentNullException.ThrowIfNull(exception);

        var traceId = Activity.Current?.TraceId.ToString() ?? UnknownTraceId;
        UiFailureLog.HandledFailure(_logger, SafeOperation(operation), exception.GetType().FullName ?? exception.GetType().Name, traceId);
        IncrementCounter();
    }

    private static string SafeOperation(string? operation)
        => !string.IsNullOrWhiteSpace(operation) && operation.Length <= MaxOperationLength && operation.All(IsAllowed)
            ? operation
            : UnknownOperation;

    private static bool IsAllowed(char value)
        => char.IsAsciiLetterOrDigit(value) || value is '.' or '_' or '-' or ':' or '/';

    private void IncrementCounter()
    {
        try
        {
            _counter?.IncrementHandled();
        }
#pragma warning disable CA1031 // A failing metric must not break failure handling; the failure is logged below.
        catch (Exception)
#pragma warning restore CA1031
        {
            UiFailureLog.CounterFailed(_logger);
        }
    }
}
