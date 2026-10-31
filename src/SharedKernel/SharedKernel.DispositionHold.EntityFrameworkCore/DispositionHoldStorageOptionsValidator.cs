using Microsoft.Extensions.Options;

namespace PandaBear.DispositionHold.EntityFrameworkCore;

/// <summary>Rejects storage options whose schema or table name is not a valid unquoted PostgreSQL identifier.</summary>
internal sealed class DispositionHoldStorageOptionsValidator : IValidateOptions<DispositionHoldStorageOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, DispositionHoldStorageOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return IsValidIdentifier(options.Schema) && IsValidIdentifier(options.TableName)
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(
                "Disposition hold storage schema and table name must be valid PostgreSQL unquoted identifiers.");
    }

    private static bool IsValidIdentifier(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Length <= 63
        && IsStart(value[0])
        && value.Skip(1).All(character => IsStart(character) || character is >= '0' and <= '9');

    private static bool IsStart(char value) => value is '_' or >= 'A' and <= 'Z' or >= 'a' and <= 'z';
}
