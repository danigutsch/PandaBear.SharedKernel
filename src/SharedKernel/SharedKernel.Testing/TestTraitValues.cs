namespace PandaBear.Testing;

/// <summary>
/// Shared trait values used by multiple test projects.
/// </summary>
public static class TestTraitValues
{
    /// <summary>
    /// Category value for endpoint-focused tests.
    /// </summary>
    public const string EndpointCategory = "endpoint";

    /// <summary>
    /// Category value for dependency-injection composition tests.
    /// </summary>
    public const string DependencyInjectionCategory = "dependency-injection";

    /// <summary>
    /// Category value for security-focused tests.
    /// </summary>
    public const string SecurityCategory = "security";

    /// <summary>
    /// Scope value for API integration tests.
    /// </summary>
    public const string ApiIntegrationScope = "api-integration";

    /// <summary>
    /// Host value for TestServer-hosted tests.
    /// </summary>
    public const string TestServerHost = "test-server";

    /// <summary>
    /// Scope value for tests that need no external services.
    /// </summary>
    public const string UnitScope = "unit";

    /// <summary>
    /// Scope value for tests that use real infrastructure.
    /// </summary>
    public const string IntegrationScope = "integration";

    /// <summary>
    /// Host value for tests that run against an Aspire application host.
    /// </summary>
    public const string AspireHost = "aspire";
}
