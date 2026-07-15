namespace SharedKernel.Observability.Npgsql.Tests;

[Testing.SerialTestJustification("Telemetry tests share in-memory meter listener state and assert emitted measurements.")]
[CollectionDefinition("PostgreSQL index health telemetry", DisableParallelization = true)]
public sealed class PostgreSqlIndexHealthTelemetryTestGroup
{
}
