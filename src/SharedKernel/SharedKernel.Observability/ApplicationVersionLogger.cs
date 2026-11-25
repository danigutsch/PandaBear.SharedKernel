using Microsoft.Extensions.Logging;

namespace SharedKernel.Observability;

/// <summary>Source-generated log messages for application version startup logging.</summary>
internal static partial class ApplicationVersionLogger
{
    [LoggerMessage(1001, LogLevel.Information, "Application {ApplicationName} version {ApplicationVersion}", EventName = "ApplicationVersion")]
    public static partial void ApplicationVersion(this ILogger logger, string applicationName, string applicationVersion);
}
