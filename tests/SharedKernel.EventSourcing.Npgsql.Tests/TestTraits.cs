namespace PandaBear.EventSourcing.Npgsql.Tests;

/// <summary>Trait names and values used to categorize the PostgreSQL event sourcing tests.</summary>
internal static class TestTraits
{
    /// <summary>The trait value marking a test as an integration test.</summary>
    public const string IntegrationScope = "integration";
    /// <summary>The trait value marking a test as needing the Aspire host.</summary>
    public const string AspireHost = "aspire";
    /// <summary>The trait name under which a test's component is recorded.</summary>
    public const string ComponentName = "Component";
    /// <summary>The component trait value for the PostgreSQL event sourcing package.</summary>
    public const string PostgreSqlEventSourcingComponent = "SharedKernel.EventSourcing.Npgsql";
}
