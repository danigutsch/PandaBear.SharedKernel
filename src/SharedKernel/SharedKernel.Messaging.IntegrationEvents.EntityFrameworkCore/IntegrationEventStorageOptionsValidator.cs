using Microsoft.Extensions.Options;

namespace SharedKernel.Messaging.IntegrationEvents.EntityFrameworkCore;

internal sealed class IntegrationEventStorageOptionsValidator : IValidateOptions<IntegrationEventStorageOptions>
{
    public ValidateOptionsResult Validate(string? name, IntegrationEventStorageOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!IsValidIdentifier(options.Schema)
            || !IsValidIdentifier(options.OutboxTableName)
            || !IsValidIdentifier(options.TransportTableName))
        {
            return ValidateOptionsResult.Fail(
                "Integration event storage schema and table names must be valid PostgreSQL unquoted identifiers.");
        }

        return string.Equals(options.OutboxTableName, options.TransportTableName, StringComparison.Ordinal)
            ? ValidateOptionsResult.Fail("Integration event outbox and transport table names must be distinct within one schema.")
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
