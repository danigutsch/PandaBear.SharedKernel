using Microsoft.Extensions.Options;
using PandaBear.InputNormalization;

namespace PandaBear.Messaging.IntegrationEvents.EntityFrameworkCore;

/// <summary>Rejects integration event storage options whose schemas or table names are not valid unquoted PostgreSQL identifiers.</summary>
internal sealed class IntegrationEventStorageOptionsValidator : IValidateOptions<IntegrationEventStorageOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, IntegrationEventStorageOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var outboxSchema = options.EffectiveOutboxSchema;
        var transportSchema = options.EffectiveTransportSchema;
        if (!PostgreSqlIdentifier.IsUnquoted(outboxSchema)
            || !PostgreSqlIdentifier.IsUnquoted(transportSchema)
            || !PostgreSqlIdentifier.IsUnquoted(options.OutboxTableName)
            || !PostgreSqlIdentifier.IsUnquoted(options.TransportTableName))
        {
            return ValidateOptionsResult.Fail(
                "Integration event storage schema and table names must be valid PostgreSQL unquoted identifiers.");
        }

        var hasDuplicateOwnership = string.Equals(outboxSchema, transportSchema, StringComparison.Ordinal)
            && string.Equals(options.OutboxTableName, options.TransportTableName, StringComparison.Ordinal);

        return hasDuplicateOwnership
            ? ValidateOptionsResult.Fail("Integration event outbox and transport schema/table ownership must be distinct.")
            : ValidateOptionsResult.Success;
    }
}
