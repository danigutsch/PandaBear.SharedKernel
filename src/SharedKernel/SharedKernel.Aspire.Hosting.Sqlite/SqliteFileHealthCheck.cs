using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Aspire.Hosting;

/// <summary>
/// Read-only health check that reports healthy when the database directory exists and the database file, if it exists, can be opened for reading.
/// It does not probe writability and never creates, modifies or deletes anything; it reports that the location exists and the file is readable, not that it is writable.
/// </summary>
/// <param name="databasePath">The absolute path of the database file.</param>
internal sealed class SqliteFileHealthCheck(string databasePath) : IHealthCheck
{
    /// <inheritdoc />
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(databasePath);
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("The SQLite database directory does not exist."));
        }

        if (File.Exists(databasePath))
        {
            try
            {
                using var stream = new FileStream(databasePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return Task.FromResult(HealthCheckResult.Unhealthy("The SQLite database file is not readable."));
            }
        }

        return Task.FromResult(HealthCheckResult.Healthy());
    }
}
