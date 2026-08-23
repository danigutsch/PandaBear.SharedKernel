using SharedKernel.Idempotency.EntityFrameworkCore;
using SharedKernel.Messaging.IntegrationEvents.EntityFrameworkCore;
using SharedKernel.Testing;

namespace SharedKernel.EntityFrameworkCore.Tests;

[Trait(SharedKernelTestTraitNames.CategoryName, TestTraits.ProviderGuardCategory)]
[Trait(SharedKernelTestTraitNames.CapabilityName, TestTraits.IntegrationEventTransportCapability)]
public sealed class IntegrationEventStorageOptionsTests
{
    [Fact]
    public void Defaults_preserve_existing_messaging_table_mappings()
    {
        // Arrange
        var integrationEventOptions = new IntegrationEventStorageOptions();
        var idempotencyOptions = new IdempotencyStorageOptions();

        // Act
        var integrationEventResult = new IntegrationEventStorageOptionsValidator().Validate(null, integrationEventOptions);
        var idempotencyResult = new IdempotencyStorageOptionsValidator().Validate(null, idempotencyOptions);

        // Assert
        integrationEventResult.Succeeded.ShouldBeTrue();
        idempotencyResult.Succeeded.ShouldBeTrue();
        integrationEventOptions.Schema.ShouldBe("messaging");
        integrationEventOptions.OutboxSchema.ShouldBeNull();
        integrationEventOptions.TransportSchema.ShouldBeNull();
        integrationEventOptions.OutboxTableName.ShouldBe("outbox_messages");
        integrationEventOptions.TransportTableName.ShouldBe("transport_messages");
        integrationEventOptions.ExcludeTransportFromMigrations.ShouldBeFalse();
        idempotencyOptions.Schema.ShouldBe("messaging");
        idempotencyOptions.TableName.ShouldBe("idempotency_keys");
    }

    [Theory]
    [InlineData("", "outbox_messages", "transport_messages")]
    [InlineData("invalid-schema", "outbox_messages", "transport_messages")]
    [InlineData("messaging", "", "transport_messages")]
    [InlineData("messaging", "outbox;drop", "transport_messages")]
    [InlineData("messaging", "outbox_messages", "")]
    [InlineData("messaging", "outbox_messages", "transport messages")]
    public void Integration_event_storage_rejects_blank_or_invalid_identifiers(
        string schema,
        string outboxTable,
        string transportTable)
    {
        // Arrange
        var options = new IntegrationEventStorageOptions
        {
            Schema = schema,
            OutboxTableName = outboxTable,
            TransportTableName = transportTable,
        };

        // Act
        var result = new IntegrationEventStorageOptionsValidator().Validate(null, options);

        // Assert
        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("identifier", StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Integration_event_storage_rejects_duplicate_effective_table_ownership()
    {
        // Arrange
        var options = new IntegrationEventStorageOptions
        {
            OutboxTableName = "messages",
            TransportTableName = "messages",
        };

        // Act
        var result = new IntegrationEventStorageOptionsValidator().Validate(null, options);

        // Assert
        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("distinct", StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("invalid-schema", null)]
    [InlineData(null, "transport schema")]
    public void Integration_event_storage_rejects_invalid_per_table_schema_overrides(
        string? outboxSchema,
        string? transportSchema)
    {
        // Arrange
        var options = new IntegrationEventStorageOptions
        {
            OutboxSchema = outboxSchema,
            TransportSchema = transportSchema,
        };

        // Act
        var result = new IntegrationEventStorageOptionsValidator().Validate(null, options);

        // Assert
        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("identifier", StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Integration_event_storage_accepts_the_same_table_name_in_distinct_effective_schemas()
    {
        // Arrange
        var options = new IntegrationEventStorageOptions
        {
            OutboxSchema = "branding",
            OutboxTableName = "messages",
            TransportSchema = "messaging",
            TransportTableName = "messages",
        };

        // Act
        var result = new IntegrationEventStorageOptionsValidator().Validate(null, options);

        // Assert
        result.Succeeded.ShouldBeTrue();
    }

    [Theory]
    [InlineData("", "idempotency_keys")]
    [InlineData("invalid-schema", "idempotency_keys")]
    [InlineData("messaging", "")]
    [InlineData("messaging", "idempotency;drop")]
    public void Idempotency_storage_rejects_blank_or_invalid_identifiers(string schema, string table)
    {
        // Arrange
        var options = new IdempotencyStorageOptions
        {
            Schema = schema,
            TableName = table,
        };

        // Act
        var result = new IdempotencyStorageOptionsValidator().Validate(null, options);

        // Assert
        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("identifier", StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Custom_storage_mappings_are_context_specific()
    {
        // Arrange
        await using var scenario = ContextQualifiedMessagingScenario.Create();

        // Act
        var first = await scenario.GetFirstStorageMappings();
        var second = await scenario.GetSecondStorageMappings();

        // Assert
        first.ShouldBe(("first_messaging", "first_outbox", "first_messaging", "first_transport", "first_messaging", "first_inbox"));
        second.ShouldBe(("second_messaging", "second_outbox", "second_messaging", "second_transport", "second_messaging", "second_inbox"));
    }

    [Fact]
    public async Task Default_registrations_do_not_reuse_transport_only_cached_model()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        _ = await ContextQualifiedMessagingScenario.PublishWithApplicationRegisteredAfterProducer(ct);

        // Act
        var mappings = await ContextQualifiedMessagingScenario.GetDefaultStorageMappings();

        // Assert
        mappings.ShouldBe(("messaging", "outbox_messages", "messaging", "transport_messages", "messaging", "idempotency_keys"));
    }

    [Fact]
    public async Task Per_table_schema_overrides_map_one_context_to_distinct_table_owners()
    {
        // Act
        var storage = await ContextQualifiedMessagingScenario.GetSplitSchemaStorage();

        // Assert
        storage.OutboxSchema.ShouldBe("branding");
        storage.OutboxTable.ShouldBe("outbox_messages");
        storage.TransportSchema.ShouldBe("messaging");
        storage.TransportTable.ShouldBe("transport_messages");
    }

    [Fact]
    public async Task Shared_transport_mapping_can_exclude_a_non_owner_context_from_migrations()
    {
        // Act
        var excludedFromMigrations = await ContextQualifiedMessagingScenario.IsSplitSchemaTransportExcludedFromMigrations();

        // Assert
        excludedFromMigrations.ShouldBeTrue();
    }

    [Fact]
    public async Task PostgreSql_claim_sql_uses_each_effective_table_schema()
    {
        // Act
        var storage = await ContextQualifiedMessagingScenario.GetSplitSchemaStorage();

        // Assert
        storage.OutboxSql.ShouldContain("UPDATE \"branding\".\"outbox_messages\"", StringComparison.Ordinal);
        storage.TransportSql.ShouldContain("UPDATE \"messaging\".\"transport_messages\"", StringComparison.Ordinal);
    }

    [Fact]
    public async Task PostgreSql_claim_sql_uses_each_contexts_validated_metadata_identifiers()
    {
        // Arrange
        await using var scenario = ContextQualifiedMessagingScenario.Create();

        // Act
        var sql = await scenario.GetClaimSql();

        // Assert
        sql.FirstOutbox.ShouldContain("UPDATE \"first_messaging\".\"first_outbox\"", StringComparison.Ordinal);
        sql.FirstTransport.ShouldContain("UPDATE \"first_messaging\".\"first_transport\"", StringComparison.Ordinal);
        sql.SecondOutbox.ShouldContain("UPDATE \"second_messaging\".\"second_outbox\"", StringComparison.Ordinal);
        sql.SecondTransport.ShouldContain("UPDATE \"second_messaging\".\"second_transport\"", StringComparison.Ordinal);
    }
}
