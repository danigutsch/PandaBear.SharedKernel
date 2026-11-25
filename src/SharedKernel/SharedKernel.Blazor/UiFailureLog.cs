using Microsoft.Extensions.Logging;

namespace SharedKernel.Blazor;

/// <summary>Source-generated log messages for handled UI failures.</summary>
internal static partial class UiFailureLog
{
    [LoggerMessage(UiFailureReporter.HandledFailureEventId, LogLevel.Error, "Handled UI failure in {Operation}: {ErrorType} (trace {TraceId})", EventName = "HandledUiFailure")]
    public static partial void HandledFailure(ILogger logger, string operation, string errorType, string traceId);
}
