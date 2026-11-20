using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Caching.Distributed;

namespace PandaBear.AspNetCore.Authentication.Bff;

/// <summary>
/// Keeps user tokens and exchanged tokens in the distributed cache, protected with Data Protection under a purpose bound
/// to each entry's key, so an entry copied to another key or session cannot be read.
/// </summary>
internal sealed class ProtectedTokenCache
{
    private const string UserTokensPurpose = "PandaBear.AspNetCore.Authentication.Bff.UserTokens.v1";
    private const string ExchangedTokenPurpose = "PandaBear.AspNetCore.Authentication.Bff.ExchangedToken.v1";
    private const byte FormatVersion = 1;
    private const int FingerprintLength = 32;

    private readonly IDistributedCache _cache;
    private readonly IUserTokenCacheConcurrency? _concurrency;
    private readonly IDataProtector _userTokensProtector;
    private readonly IDataProtector _exchangedTokenProtector;
    private readonly string _keyPrefix;

    /// <summary>Initializes a new instance of the <see cref="ProtectedTokenCache" /> class.</summary>
    /// <param name="cache">The distributed cache.</param>
    /// <param name="dataProtectionProvider">Protects every entry.</param>
    /// <param name="keyPrefix">The prefix of every key.</param>
    /// <param name="concurrency">Optional atomic conditional writes and removals for user tokens.</param>
    internal ProtectedTokenCache(
        IDistributedCache cache,
        IDataProtectionProvider dataProtectionProvider,
        string keyPrefix,
        IUserTokenCacheConcurrency? concurrency = null)
    {
        _cache = cache;
        _concurrency = concurrency;
        _userTokensProtector = dataProtectionProvider.CreateProtector(UserTokensPurpose);
        _exchangedTokenProtector = dataProtectionProvider.CreateProtector(ExchangedTokenPurpose);
        _keyPrefix = keyPrefix;
    }

    /// <summary>Reads the tokens of a session; an unreadable entry is removed and reads as missing.</summary>
    /// <param name="session">The session.</param>
    /// <param name="ct">Cancels the read.</param>
    /// <returns>The tokens, or <see langword="null" />.</returns>
    internal async Task<UserTokenSet?> GetUserTokens(UserSession session, CancellationToken ct)
    {
        var entry = await GetUserTokenVersion(session, ct).ConfigureAwait(false);
        return entry?.Tokens;
    }

    /// <summary>Reads a session's tokens and the protected cache value used for conditional mutations.</summary>
    internal async Task<(UserTokenSet Tokens, byte[] Version)?> GetUserTokenVersion(UserSession session, CancellationToken ct)
    {
        var key = UserTokensKey(session);
        var stored = await _cache.GetAsync(key, ct).ConfigureAwait(false);
        if (stored is null)
        {
            return null;
        }

        var tokens = TryRead(_userTokensProtector, key, stored, ReadUserTokens);
        if (tokens is not null)
        {
            return (tokens, stored);
        }

        _ = await TryRemoveUserTokens(session, stored, ct).ConfigureAwait(false);
        return null;
    }

    /// <summary>Writes the tokens of a session; the entry expires with the session.</summary>
    /// <param name="session">The session.</param>
    /// <param name="tokens">The tokens.</param>
    /// <param name="ct">Cancels the write.</param>
    /// <returns>A task that completes when the entry is written.</returns>
    internal Task SetUserTokens(UserSession session, UserTokenSet tokens, CancellationToken ct)
    {
        var key = UserTokensKey(session);
        return _cache.SetAsync(
            key,
            ProtectUserTokens(key, tokens),
            new DistributedCacheEntryOptions { AbsoluteExpiration = session.ExpiresAt },
            ct);
    }

    /// <summary>Replaces tokens only while the cache still contains the expected protected entry.</summary>
    internal async Task<bool> TryReplaceUserTokens(UserSession session, byte[] expectedValue, UserTokenSet tokens, CancellationToken ct)
    {
        var key = UserTokensKey(session);
        if (_concurrency is not null)
        {
            return await _concurrency.TryReplace(key, expectedValue, ProtectUserTokens(key, tokens), session.ExpiresAt, ct)
                .ConfigureAwait(false);
        }

        if (!await HasUserTokenVersion(key, expectedValue, ct).ConfigureAwait(false))
        {
            return false;
        }

        await SetUserTokens(session, tokens, ct).ConfigureAwait(false);
        return true;
    }

    /// <summary>Removes tokens only while the cache still contains the expected protected entry.</summary>
    internal async Task<bool> TryRemoveUserTokens(UserSession session, byte[] expectedValue, CancellationToken ct)
    {
        var key = UserTokensKey(session);
        if (_concurrency is not null)
        {
            return await _concurrency.TryRemove(key, expectedValue, ct).ConfigureAwait(false);
        }

        if (!await HasUserTokenVersion(key, expectedValue, ct).ConfigureAwait(false))
        {
            return false;
        }

        await _cache.RemoveAsync(key, ct).ConfigureAwait(false);
        return true;
    }

    private async Task<bool> HasUserTokenVersion(string key, byte[] expectedValue, CancellationToken ct)
    {
        var current = await _cache.GetAsync(key, ct).ConfigureAwait(false);
        return current is not null && CryptographicOperations.FixedTimeEquals(expectedValue, current);
    }

    private byte[] ProtectUserTokens(string key, UserTokenSet tokens)
    {
        var plaintext = Write(writer =>
        {
            writer.Write(tokens.AccessToken);
            writer.Write(tokens.AccessTokenExpiresAt.ToUnixTimeMilliseconds());
            WriteOptional(writer, tokens.RefreshToken);
            WriteOptional(writer, tokens.IdentityToken);
            WriteOptional(writer, tokens.AccessTokenType);
            WriteOptional(writer, tokens.Scope);
        });
        return _userTokensProtector.CreateProtector(key).Protect(plaintext);
    }

    /// <summary>Removes the tokens of a session.</summary>
    /// <param name="session">The session.</param>
    /// <param name="ct">Cancels the removal.</param>
    /// <returns>A task that completes when the entry is removed.</returns>
    internal Task RemoveUserTokens(UserSession session, CancellationToken ct)
    {
        return _cache.RemoveAsync(UserTokensKey(session), ct);
    }

    /// <summary>Reads the exchanged token of a session and target, if it was issued for the given source token.</summary>
    /// <param name="session">The session.</param>
    /// <param name="target">The exchange target.</param>
    /// <param name="sourceAccessToken">The user access token the exchanged token must stem from.</param>
    /// <param name="ct">Cancels the read.</param>
    /// <returns>The exchanged token and its expiry, or <see langword="null" />.</returns>
    internal async Task<(string AccessToken, DateTimeOffset ExpiresAt)?> GetExchangedToken(
        UserSession session,
        TokenExchangeTarget target,
        string sourceAccessToken,
        CancellationToken ct)
    {
        var key = ExchangedTokenKey(session, target);
        var stored = await _cache.GetAsync(key, ct).ConfigureAwait(false);
        if (stored is null)
        {
            return null;
        }

        var entry = TryRead(_exchangedTokenProtector, key, stored, ReadExchangedToken);
        if (entry is null)
        {
            await _cache.RemoveAsync(key, ct).ConfigureAwait(false);
            return null;
        }

        return CryptographicOperations.FixedTimeEquals(entry.Fingerprint, Fingerprint(sourceAccessToken))
            ? (entry.AccessToken, entry.ExpiresAt)
            : null;
    }

    /// <summary>Writes an exchanged token; the entry expires with the token or the session, whichever is first.</summary>
    /// <param name="session">The session.</param>
    /// <param name="target">The exchange target.</param>
    /// <param name="sourceAccessToken">The user access token that was exchanged; only its SHA-256 fingerprint is kept.</param>
    /// <param name="accessToken">The exchanged token.</param>
    /// <param name="expiresAt">When the exchanged token expires.</param>
    /// <param name="ct">Cancels the write.</param>
    /// <returns>A task that completes when the entry is written.</returns>
    internal Task SetExchangedToken(
        UserSession session,
        TokenExchangeTarget target,
        string sourceAccessToken,
        string accessToken,
        DateTimeOffset expiresAt,
        CancellationToken ct)
    {
        var key = ExchangedTokenKey(session, target);
        var plaintext = Write(writer =>
        {
            writer.Write(Fingerprint(sourceAccessToken));
            writer.Write(accessToken);
            writer.Write(expiresAt.ToUnixTimeMilliseconds());
        });
        return _cache.SetAsync(
            key,
            _exchangedTokenProtector.CreateProtector(key).Protect(plaintext),
            new DistributedCacheEntryOptions { AbsoluteExpiration = expiresAt < session.ExpiresAt ? expiresAt : session.ExpiresAt },
            ct);
    }

    /// <summary>Removes the exchanged token of a session and target.</summary>
    /// <param name="session">The session.</param>
    /// <param name="target">The exchange target.</param>
    /// <param name="ct">Cancels the removal.</param>
    /// <returns>A task that completes when the entry is removed.</returns>
    internal Task RemoveExchangedToken(UserSession session, TokenExchangeTarget target, CancellationToken ct)
    {
        return _cache.RemoveAsync(ExchangedTokenKey(session, target), ct);
    }

    private string UserTokensKey(UserSession session)
    {
        return string.Concat(_keyPrefix, "user-tokens:", session.Id);
    }

    private string ExchangedTokenKey(UserSession session, TokenExchangeTarget target)
    {
        var targetHash = SHA256.HashData(Encoding.UTF8.GetBytes(string.Concat(target.Audience, "\u001f", target.Scope)));
        return string.Concat(_keyPrefix, "exchanged-token:", session.Id, ":", WebEncoders.Base64UrlEncode(targetHash));
    }

    private static byte[] Fingerprint(string token)
    {
        return SHA256.HashData(Encoding.UTF8.GetBytes(token));
    }

    private static T? TryRead<T>(IDataProtector protector, string key, byte[] stored, Func<BinaryReader, T> read)
        where T : class
    {
        try
        {
            using var stream = new MemoryStream(protector.CreateProtector(key).Unprotect(stored));
            using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
            if (reader.ReadByte() != FormatVersion)
            {
                return null;
            }

            var value = read(reader);
            return stream.Position == stream.Length ? value : null;
        }
        catch (Exception exception) when (exception is CryptographicException or EndOfStreamException or FormatException
            or InvalidDataException or ArgumentException or OverflowException)
        {
            return null;
        }
    }

    private static UserTokenSet ReadUserTokens(BinaryReader reader)
    {
        var accessToken = reader.ReadString();
        var expiresAt = DateTimeOffset.FromUnixTimeMilliseconds(reader.ReadInt64());
        var refreshToken = ReadOptional(reader);
        var identityToken = ReadOptional(reader);
        var accessTokenType = ReadOptional(reader);
        var scope = ReadOptional(reader);
        return new UserTokenSet(accessToken, expiresAt, refreshToken, identityToken, accessTokenType, scope);
    }

    private static ExchangedEntry ReadExchangedToken(BinaryReader reader)
    {
        var fingerprint = reader.ReadBytes(FingerprintLength);
        if (fingerprint.Length != FingerprintLength)
        {
            throw new EndOfStreamException();
        }

        var accessToken = reader.ReadString();
        var expiresAt = DateTimeOffset.FromUnixTimeMilliseconds(reader.ReadInt64());
        return string.IsNullOrWhiteSpace(accessToken)
            ? throw new InvalidDataException()
            : new ExchangedEntry(fingerprint, accessToken, expiresAt);
    }

    private static byte[] Write(Action<BinaryWriter> write)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(FormatVersion);
            write(writer);
        }

        return stream.ToArray();
    }

    private static void WriteOptional(BinaryWriter writer, string? value)
    {
        writer.Write(value is not null);
        if (value is not null)
        {
            writer.Write(value);
        }
    }

    private static string? ReadOptional(BinaryReader reader)
    {
        return reader.ReadBoolean() ? reader.ReadString() : null;
    }

    private sealed record ExchangedEntry(byte[] Fingerprint, string AccessToken, DateTimeOffset ExpiresAt)
    {
        public override string ToString()
        {
            return "ExchangedEntry { token redacted }";
        }
    }
}
