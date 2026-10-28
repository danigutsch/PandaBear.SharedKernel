using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;

namespace PandaBear.Blazor;

/// <summary>
/// Logs handled UI failures with only the exception type name, the operation name, and the trace id, then counts them.
/// </summary>
/// <remarks>
/// The exception object, its message, its stack, and its data are never logged or used as a tag. Metrics are fail-safe:
/// a failing meter factory or listener is written to standard error as the failure type name only and never breaks
/// reporting or the log. Operation names that are blank,
/// longer than <see cref="MaxOperationLength" />, or contain characters other than letters, digits, and
/// <c>. _ - : /</c> are replaced by <see cref="UnknownOperation" />.
/// </remarks>
public sealed class UiFailureReporter : IUiFailureReporter
{
    /// <summary>The stable <see cref="EventId" /> number of the handled-failure log entry.</summary>
    public const int HandledFailureEventId = 7001;

    /// <summary>The longest operation name that is logged unchanged.</summary>
    public const int MaxOperationLength = 100;

    /// <summary>The operation logged when the supplied name is not safe.</summary>
    public const string UnknownOperation = "unknown";

    /// <summary>The trace id logged when no <see cref="Activity" /> is current.</summary>
    public const string UnknownTraceId = "unknown";

    /// <summary>The name of the meter; register it with your OpenTelemetry metrics pipeline.</summary>
    public const string MeterName = "PandaBear.Blazor";

    /// <summary>The name of the counter of handled UI failures.</summary>
    public const string HandledFailuresCounterName = "handled_ui_failures_total";

    /// <summary>The counter tag that carries the exception type name.</summary>
    public const string ErrorTypeTag = "error_type";

    /// <summary>The counter tag that carries the sanitized operation name.</summary>
    public const string OperationTag = "operation";

    private readonly ILogger<UiFailureReporter> _logger;
    private Counter<long>? _counter;

    /// <summary>Initializes a reporter.</summary>
    /// <param name="logger">The logger.</param>
    /// <param name="meterFactory">The factory that creates the package meter.</param>
    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "IMeterFactory owns and disposes the meter.")]
    public UiFailureReporter(ILogger<UiFailureReporter> logger, IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(meterFactory);

        _logger = logger;
        TelemetryGuard.Run(() => _counter = meterFactory.Create(MeterName)
            .CreateCounter<long>(
                HandledFailuresCounterName,
                unit: "{failure}",
                description: "Handled UI failures by exception type and operation."));
    }

    /// <inheritdoc />
    public void Report(Exception exception, string operation)
    {
        ArgumentNullException.ThrowIfNull(exception);

        var traceId = Activity.Current?.TraceId.ToString() ?? UnknownTraceId;
        var operationName = SafeOperation(operation);
        UiFailureLog.HandledFailure(_logger, operationName, exception.GetType().FullName ?? exception.GetType().Name, traceId);
        var errorType = ErrorType(exception);
        TelemetryGuard.Run(() => _counter?.Add(
            1,
            new KeyValuePair<string, object?>(ErrorTypeTag, errorType),
            new KeyValuePair<string, object?>(OperationTag, operationName)));
    }

    private static string ErrorType(Exception exception) => UnwrapWrapperExceptions(exception).GetType().Name;

    /// <summary>Returns the first inner exception of aggregate and reflection wrappers, which hide the real fault; tagging with it keeps the tag bounded and meaningful.</summary>
    private static Exception UnwrapWrapperExceptions(Exception exception)
    {
        while (exception is AggregateException or System.Reflection.TargetInvocationException && exception.InnerException is { } inner)
        {
            exception = inner;
        }

        return exception;
    }

    private static string SafeOperation(string? operation)
        => !string.IsNullOrWhiteSpace(operation) && operation.Length <= MaxOperationLength && operation.All(IsAllowed)
            ? operation
            : UnknownOperation;

    private static bool IsAllowed(char value)
        => char.IsAsciiLetterOrDigit(value) || value is '.' or '_' or '-' or ':' or '/';
}
