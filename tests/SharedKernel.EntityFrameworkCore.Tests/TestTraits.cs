namespace PandaBear.EntityFrameworkCore.Tests;

/// <summary>
/// xUnit trait names and values used to categorize the EntityFrameworkCore tests.
/// </summary>
internal static class TestTraits
{
    /// <summary>
    /// Category value for tests that exercise behavior without a real database.
    /// </summary>
    public const string CoreBehaviorCategory = "core-behavior";

    /// <summary>
    /// Category value for tests that need a real database such as the PostgreSQL fixture.
    /// </summary>
    public const string DatabaseIntegrationCategory = "database-integration";

    /// <summary>
    /// Capability value for tests of the integration event transport.
    /// </summary>
    public const string IntegrationEventTransportCapability = "integration-event-transport";

    /// <summary>
    /// Capability value for tests of the idempotency store.
    /// </summary>
    public const string IdempotencyCapability = "idempotency";

    /// <summary>
    /// Capability value for tests of the PostgreSQL fixture itself.
    /// </summary>
    public const string PostgreSqlFixtureCapability = "postgresql-fixture";

    /// <summary>
    /// Capability value for tests of the command transaction scope.
    /// </summary>
    public const string CommandTransactionScopeCapability = "command-transaction-scope";

    /// <summary>
    /// Capability value for tests of the result transaction pipeline behavior.
    /// </summary>
    public const string ResultTransactionBehaviorCapability = "result-transaction-behavior";

    /// <summary>
    /// Capability value for tests of domain event dispatch.
    /// </summary>
    public const string DomainEventDispatchCapability = "domain-event-dispatch";

    /// <summary>
    /// Capability value for tests of the disposition hold store and guard.
    /// </summary>
    public const string DispositionHoldCapability = "disposition-hold";

    /// <summary>
    /// Category value for tests that check provider guard behavior.
    /// </summary>
    public const string ProviderGuardCategory = "provider-guard";
}
