using Microsoft.Extensions.Logging;

namespace PandaBear.Testing.Tests;

/// <summary>The log entries the tests of <see cref="TestOutputLoggerProvider"/> write.</summary>
internal static partial class SampleLogs
{
    /// <summary>Logs that an order is late.</summary>
    /// <param name="logger">The logger.</param>
    /// <param name="orderId">The order.</param>
    [LoggerMessage(EventId = 7, Level = LogLevel.Warning, Message = "Order {OrderId} is late")]
    internal static partial void OrderLate(ILogger logger, int orderId);

    /// <summary>Logs that a payment failed.</summary>
    /// <param name="logger">The logger.</param>
    /// <param name="exception">The failure.</param>
    [LoggerMessage(EventId = 0, Level = LogLevel.Error, Message = "Payment failed")]
    internal static partial void PaymentFailed(ILogger logger, Exception exception);

    /// <summary>Logs a request.</summary>
    /// <param name="logger">The logger.</param>
    /// <param name="path">The requested path.</param>
    [LoggerMessage(EventId = 0, Level = LogLevel.Information, Message = "GET {Path}")]
    internal static partial void Request(ILogger logger, string path);
}
