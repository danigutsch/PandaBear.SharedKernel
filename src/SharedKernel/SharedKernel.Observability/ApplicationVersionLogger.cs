using Microsoft.Extensions.Logging;

namespace PandaBear.Observability;

/// <summary>Source-generated log messages for application version startup logging.</summary>
internal static partial class ApplicationVersionLogger
{
    [LoggerMessage(1, LogLevel.Information, "Application {ApplicationName} version {ApplicationVersion}")]
    public static partial void ApplicationVersion(this ILogger logger, string applicationName, string applicationVersion);
}
