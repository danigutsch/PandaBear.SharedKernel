using Microsoft.Extensions.Options;

namespace SharedKernel.Idempotency.EntityFrameworkCore;

internal sealed class IdempotencyStorageOptionsValidator : IValidateOptions<IdempotencyStorageOptions>
{
    public ValidateOptionsResult Validate(string? name, IdempotencyStorageOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return IsValidIdentifier(options.Schema) && IsValidIdentifier(options.TableName)
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(
                "Idempotency storage schema and table name must be valid PostgreSQL unquoted identifiers.");
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
