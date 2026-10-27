namespace PandaBear.Observability.Npgsql;

/// <summary>
/// Carries the monitored connection strings and their shared options to the hosted service.
/// </summary>
/// <param name="connectionStrings">Connection strings of the monitored databases.</param>
/// <param name="options">Options applied to every connection string.</param>
internal sealed class PostgreSqlIndexHealthMonitoringRegistration(
    IReadOnlyList<string> connectionStrings,
    PostgreSqlIndexHealthMonitoringOptions options)
{
    /// <summary>Connection strings of the PostgreSQL databases whose indexes are monitored.</summary>
    public IReadOnlyList<string> ConnectionStrings { get; } = connectionStrings;

    /// <summary>Monitoring options that apply to the registered connection strings.</summary>
    public PostgreSqlIndexHealthMonitoringOptions Options { get; } = options;
}
