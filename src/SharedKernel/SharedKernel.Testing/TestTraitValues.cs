namespace SharedKernel.Testing;

/// <summary>
/// Shared trait values used by multiple test projects.
/// </summary>
public static class TestTraitValues
{
    /// <summary>
    /// Category value for tests that verify a contract.
    /// </summary>
    public const string ContractCategory = "contract";

    /// <summary>
    /// Category value for tests that check for drift from a reviewed artifact or contract.
    /// </summary>
    public const string DriftGuardCategory = "drift-guard";

    /// <summary>
    /// Category value for tests that compare results with an approved snapshot.
    /// </summary>
    public const string SnapshotCategory = "snapshot";

    /// <summary>
    /// Surface value for OpenAPI tests.
    /// </summary>
    public const string OpenApiSurface = "openapi";

    /// <summary>
    /// Category or capability value for tests of integration event transport.
    /// </summary>
    public const string IntegrationEventTransport = "integration-event-transport";

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
}
