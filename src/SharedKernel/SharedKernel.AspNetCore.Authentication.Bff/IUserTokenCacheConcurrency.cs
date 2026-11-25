using Microsoft.Extensions.Caching.Distributed;

namespace SharedKernel.AspNetCore.Authentication.Bff;

/// <summary>Performs atomic conditional mutations on the protected user-token entries in an <see cref="IDistributedCache" />.</summary>
/// <remarks>
/// The adapter must use the same backing store as the token cache. The exact previously read protected bytes
/// serve as an opaque version. Cache values and version bytes may contain protected token material.
/// </remarks>
public interface IUserTokenCacheConcurrency
{
    /// <summary>Atomically replaces an existing unexpired entry only if its protected value still matches.</summary>
    /// <param name="key">The cache key.</param>
    /// <param name="expectedValue">The previously read protected value.</param>
    /// <param name="replacement">The new protected value.</param>
    /// <param name="expiresAt">The absolute expiration of the replacement.</param>
    /// <param name="ct">Cancels the mutation.</param>
    /// <returns>Whether the existing entry was replaced. Returns false for an absent, expired or changed entry or an elapsed replacement expiration.</returns>
    Task<bool> TryReplace(
        string key,
        ReadOnlyMemory<byte> expectedValue,
        ReadOnlyMemory<byte> replacement,
        DateTimeOffset expiresAt,
        CancellationToken ct);

    /// <summary>Atomically removes an entry only if its protected value still matches.</summary>
    /// <param name="key">The cache key.</param>
    /// <param name="expectedValue">The previously read protected value.</param>
    /// <param name="ct">Cancels the mutation.</param>
    /// <returns>Whether the existing entry was removed.</returns>
    Task<bool> TryRemove(string key, ReadOnlyMemory<byte> expectedValue, CancellationToken ct);
}
