namespace SharedKernel.Observability.Npgsql.Tests;

internal static class TestTraits
{
    public const string UnitScope = "unit";
    public const string IntegrationScope = "integration";
    public const string ComponentName = "Component";
    public const string PostgreSqlObservabilityComponent = "SharedKernel.Observability.Npgsql";
}
