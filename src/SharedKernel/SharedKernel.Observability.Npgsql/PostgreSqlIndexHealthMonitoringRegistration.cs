namespace SharedKernel.Observability.Npgsql;

internal sealed class PostgreSqlIndexHealthMonitoringRegistration(
    IReadOnlyList<string> connectionStrings,
    PostgreSqlIndexHealthMonitoringOptions options)
{
    public IReadOnlyList<string> ConnectionStrings { get; } = connectionStrings;

    public PostgreSqlIndexHealthMonitoringOptions Options { get; } = options;
}
