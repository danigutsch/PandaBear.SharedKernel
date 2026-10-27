namespace PandaBear.Observability.Npgsql.Tests;

/// <summary>Trait names and values used to categorize the PostgreSQL observability tests.</summary>
internal static class TestTraits
{
    /// <summary>Trait value for tests that need no external services.</summary>
    public const string UnitScope = "unit";
    /// <summary>Trait value for tests that attempt a real connection.</summary>
    public const string IntegrationScope = "integration";
    /// <summary>Trait name under which the tested component is recorded.</summary>
    public const string ComponentName = "Component";
    /// <summary>Trait value naming the SharedKernel.Observability.Npgsql component.</summary>
    public const string PostgreSqlObservabilityComponent = "SharedKernel.Observability.Npgsql";
}
