using PandaBear.Idempotency;
using PandaBear.Results;

namespace PandaBear.AspNetCore;

/// <summary>Creates an idempotency key from a required HTTP header without throwing for invalid input.</summary>
public static class IdempotencyKeyHeader
{
    /// <summary>Identifies the standard idempotency key request header and validation field.</summary>
    public const string Name = "Idempotency-Key";

    /// <summary>Creates a key using the value object's token rules and normalization.</summary>
    /// <param name="value">The raw header value, or <see langword="null" /> when the header is missing.</param>
    /// <returns>The valid key, or an invalid result with errors associated with <see cref="Name" />.</returns>
    /// <remarks>
    /// Missing values produce a required-field error; present but invalid values produce a format error.
    /// This factory does not generate keys, claim operations, calculate fingerprints, or log header values.
    /// </remarks>
    public static Result<IdempotencyKey> Create(string? value)
    {
        if (value is null)
        {
            return Result.Invalid<IdempotencyKey>(
                detail: $"{Name} is required.",
                field: Name,
                message: $"{Name} is required.");
        }

        return IdempotencyKey.TryFrom(value, out var key)
            ? Result.Ok(key)
            : Result.Invalid<IdempotencyKey>(
                detail: $"{Name} must use the supported opaque token format.",
                field: Name,
                message: $"{Name} is invalid.");
    }
}
