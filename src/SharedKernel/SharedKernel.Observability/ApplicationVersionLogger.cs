using Microsoft.Extensions.Logging;

namespace SharedKernel.Observability;

internal static partial class ApplicationVersionLogger
{
    [LoggerMessage(1, LogLevel.Information, "Application {ApplicationName} version {ApplicationVersion}")]
    public static partial void ApplicationVersion(this ILogger logger, string applicationName, string applicationVersion);
}
