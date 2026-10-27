using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace PandaBear.AspNetCore;

/// <summary>
/// Keeps telemetry from breaking the host: a failing logger, meter or listener is surfaced on standard error (and to a
/// debugger) with only the exception type name, and the caller continues.
/// </summary>
internal static class TelemetryGuard
{
    /// <summary>Runs a telemetry action and contains any non-fatal failure.</summary>
    /// <param name="action">The telemetry action.</param>
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Telemetry must never break the request flow; the failure is surfaced, not swallowed.")]
    public static void Run(Action action)
    {
        try
        {
            action();
        }
        catch (Exception exception) when (exception is not (OutOfMemoryException or AccessViolationException))
        {
            Surface(exception.GetType().Name);
        }
    }

    private static void Surface(string exceptionType)
    {
        var line = $"PandaBear.AspNetCore telemetry failure: {exceptionType}";
        try
        {
            Console.Error.WriteLine(line);
        }
        catch (IOException)
        {
            // Standard error is the last resort; when it is closed there is nowhere left to report.
        }

        if (Debugger.IsAttached)
        {
            Debug.Fail(line);
        }
    }
}
