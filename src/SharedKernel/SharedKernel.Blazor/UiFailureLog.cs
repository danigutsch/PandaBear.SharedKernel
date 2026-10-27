using Microsoft.Extensions.Logging;

namespace PandaBear.Blazor;

/// <summary>Source-generated log messages for handled UI failures.</summary>
internal static partial class UiFailureLog
{
    [LoggerMessage(UiFailureReporter.HandledFailureEventId, LogLevel.Error, "Handled UI failure in {Operation}: {ExceptionType} (trace {TraceId})", EventName = "HandledUiFailure")]
    public static partial void HandledFailure(ILogger logger, string operation, string exceptionType, string traceId);
}
