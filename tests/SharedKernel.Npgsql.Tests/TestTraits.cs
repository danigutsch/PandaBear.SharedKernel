namespace PandaBear.Npgsql.Tests;

/// <summary>Trait values used to categorize the PostgreSQL tests.</summary>
internal static class TestTraits
{
    /// <summary>The trait value marking a test as an integration test.</summary>
    public const string IntegrationScope = "integration";
    /// <summary>The trait value marking a test as needing the Aspire host.</summary>
    public const string AspireHost = "aspire";
}
