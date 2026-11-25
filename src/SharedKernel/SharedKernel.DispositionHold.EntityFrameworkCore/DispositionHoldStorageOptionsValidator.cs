using Microsoft.Extensions.Options;
using SharedKernel.PostgreSql;

namespace SharedKernel.DispositionHold.EntityFrameworkCore;

/// <summary>Rejects storage options whose schema or table name is not a valid unquoted PostgreSQL identifier.</summary>
internal sealed class DispositionHoldStorageOptionsValidator : IValidateOptions<DispositionHoldStorageOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, DispositionHoldStorageOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return PostgreSqlIdentifier.IsUnquoted(options.Schema) && PostgreSqlIdentifier.IsUnquoted(options.TableName)
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(
                "Disposition hold storage schema and table name must be valid PostgreSQL unquoted identifiers.");
    }
}
