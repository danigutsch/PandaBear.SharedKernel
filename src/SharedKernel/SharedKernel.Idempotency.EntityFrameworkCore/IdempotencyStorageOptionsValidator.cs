using Microsoft.Extensions.Options;
using SharedKernel.InputNormalization;

namespace SharedKernel.Idempotency.EntityFrameworkCore;

/// <summary>Rejects idempotency storage options whose schema or table name is not a valid unquoted PostgreSQL identifier.</summary>
internal sealed class IdempotencyStorageOptionsValidator : IValidateOptions<IdempotencyStorageOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, IdempotencyStorageOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return PostgreSqlIdentifier.IsUnquoted(options.Schema) && PostgreSqlIdentifier.IsUnquoted(options.TableName)
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(
                "Idempotency storage schema and table name must be valid PostgreSQL unquoted identifiers.");
    }
}
