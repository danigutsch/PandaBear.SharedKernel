using Microsoft.Extensions.Caching.Distributed;

namespace SharedKernel.AspNetCore.Authentication.Bff.Tests;

/// <summary>Conditionally mutates protected values in the shared recording cache and records the attempts.</summary>
internal sealed class RecordingUserTokenCacheConcurrency(RecordingDistributedCache cache, TimeProvider clock) : IUserTokenCacheConcurrency
{
    /// <summary>Gets or sets an intervening cache change that runs immediately before the comparison.</summary>
    internal Action<string>? BeforeCompare { get; set; }

    /// <summary>Gets the last replacement expiration, or null when none was attempted.</summary>
    internal DateTimeOffset? ReplacementExpiration { get; private set; }

    /// <summary>Gets the number of conditional removals attempted.</summary>
    internal int RemovalAttempts { get; private set; }

    /// <inheritdoc />
    public Task<bool> TryReplace(
        string key,
        ReadOnlyMemory<byte> expectedValue,
        ReadOnlyMemory<byte> replacement,
        DateTimeOffset expiresAt,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        ReplacementExpiration = expiresAt;
        BeforeCompare?.Invoke(key);
        var now = clock.GetUtcNow();
        if (!cache.Entries.TryGetValue(key, out var current) || !expectedValue.Span.SequenceEqual(current)
            || !cache.Options.TryGetValue(key, out var options) || options.AbsoluteExpiration <= now || expiresAt <= now
            || !cache.Entries.TryUpdate(key, replacement.ToArray(), current))
        {
            return Task.FromResult(false);
        }

        cache.Options[key] = new DistributedCacheEntryOptions { AbsoluteExpiration = expiresAt };
        return Task.FromResult(true);
    }

    /// <inheritdoc />
    public Task<bool> TryRemove(string key, ReadOnlyMemory<byte> expectedValue, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        RemovalAttempts++;
        BeforeCompare?.Invoke(key);
        return Task.FromResult(cache.Entries.TryGetValue(key, out var current) && expectedValue.Span.SequenceEqual(current)
            && cache.Entries.TryRemove(new KeyValuePair<string, byte[]>(key, current)));
    }
}
