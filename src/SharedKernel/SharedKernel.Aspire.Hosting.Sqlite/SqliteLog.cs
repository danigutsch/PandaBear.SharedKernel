using Microsoft.Extensions.Logging;

namespace Aspire.Hosting;

/// <summary>
/// Log messages of the SQLite hosting integration.
/// </summary>
internal static partial class SqliteLog
{
    /// <summary>
    /// Logs that the ephemeral database directory could not be deleted when the AppHost stopped, so it stays in the temp folder.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="directory">The directory that was left behind.</param>
    /// <param name="exception">The deletion failure.</param>
    [LoggerMessage(EventId = 9001, EventName = "EphemeralDirectoryNotDeleted", Level = LogLevel.Warning, Message = "The ephemeral SQLite directory '{Directory}' could not be deleted; delete it by hand.")]
    public static partial void EphemeralDirectoryNotDeleted(ILogger logger, string directory, Exception exception);
}
