using Microsoft.Extensions.Options;

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
        if (!IsValidIdentifier(outboxSchema)
            || !IsValidIdentifier(transportSchema)
            || !IsValidIdentifier(options.OutboxTableName)
            || !IsValidIdentifier(options.TransportTableName))
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

    private static bool IsValidIdentifier(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 63 || !IsIdentifierStart(value[0]))
        {
            return false;
        }

        for (var index = 1; index < value.Length; index++)
        {
            if (!IsIdentifierPart(value[index]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsIdentifierStart(char value) => value is '_' or >= 'A' and <= 'Z' or >= 'a' and <= 'z';

    private static bool IsIdentifierPart(char value) => IsIdentifierStart(value) || value is >= '0' and <= '9';
}
