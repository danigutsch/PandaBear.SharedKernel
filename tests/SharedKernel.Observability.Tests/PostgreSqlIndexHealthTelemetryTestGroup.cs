namespace PandaBear.Observability.Npgsql.Tests;

/// <summary>Collection definition that runs the PostgreSQL index health telemetry tests serially because they share meter listener state.</summary>
[Testing.SerialTestJustification("Telemetry tests share in-memory meter listener state and assert emitted measurements.")]
[CollectionDefinition("PostgreSQL index health telemetry", DisableParallelization = true)]
public sealed class PostgreSqlIndexHealthTelemetryTestGroup
{
}
