namespace PandaBear.Observability.Npgsql;

internal sealed class PostgreSqlIndexHealthMonitoringRegistration(
    IReadOnlyList<string> connectionStrings,
    PostgreSqlIndexHealthMonitoringOptions options)
{
    /// <summary>Connection strings of the PostgreSQL databases whose indexes are monitored.</summary>
    public IReadOnlyList<string> ConnectionStrings { get; } = connectionStrings;

    /// <summary>Monitoring options that apply to the registered connection strings.</summary>
    public PostgreSqlIndexHealthMonitoringOptions Options { get; } = options;
}
