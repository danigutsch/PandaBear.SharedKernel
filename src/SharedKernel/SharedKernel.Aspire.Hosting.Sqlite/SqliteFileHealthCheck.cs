using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Aspire.Hosting;

/// <summary>
/// Read-only health check that reports healthy when the database directory exists and the database file, if it exists, can be opened for reading.
/// It does not probe writability and never creates, modifies or deletes anything; it reports that the location exists and the file is readable, not that it is writable.
/// </summary>
/// <param name="resource">The database resource, read at check time so a later <c>WithDataBindMount</c> is honored.</param>
internal sealed class SqliteFileHealthCheck(SqliteResource resource) : IHealthCheck
{
    /// <inheritdoc />
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(resource.DataDirectory))
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("The SQLite database directory does not exist."));
        }

        var databasePath = resource.DatabasePath;
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
