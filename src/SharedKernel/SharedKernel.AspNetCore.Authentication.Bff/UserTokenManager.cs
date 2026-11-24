using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace PandaBear.AspNetCore.Authentication.Bff;

/// <summary>
/// Keeps the tokens of each signed-in session server-side, refreshes the access token before it expires, exchanges it for
/// other audiences (RFC 8693) and ends the session at sign-out.
/// </summary>
/// <remarks>
/// <para>
/// Tokens live in the <see cref="IDistributedCache" />, protected with Data Protection under a purpose bound to each entry's
/// key; a session is identified by the opaque <see cref="BffClaimTypes.SessionId" /> claim. Refreshes, exchanges and sign-out
/// of one session are serialized by <see cref="IUserSessionLock" />, so concurrent callers share one call to the identity
/// provider and a rotated refresh token is redeemed once.
/// </para>
/// <para>Tokens are never logged: log events carry only the operation, an HTTP status and an OAuth error code.</para>
/// </remarks>
public sealed partial class UserTokenManager
{
    private const string BearerTokenType = "Bearer";
    private const string AccessTokenTypeIdentifier = "urn:ietf:params:oauth:token-type:access_token";
    private static readonly long MaximumLifetimeSeconds = (long)TimeSpan.FromDays(366).TotalSeconds;

    private readonly ProtectedTokenCache _store;
    private readonly IdentityProviderClient _identityProvider;
    private readonly UserTokenManagementOptions _options;
    private readonly IUserSessionLock _sessionLock;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<UserTokenManager> _logger;

    /// <summary>Initializes a new instance of the <see cref="UserTokenManager" /> class.</summary>
    /// <param name="cache">The distributed cache that holds the protected tokens.</param>
    /// <param name="dataProtectionProvider">Protects the tokens at rest.</param>
    /// <param name="httpClientFactory">Creates the <see cref="UserTokenManagementDefaults.IdentityProviderHttpClientName" /> client.</param>
    /// <param name="openIdConnectOptions">Supplies the client credentials and the discovery document.</param>
    /// <param name="options">The token management options.</param>
    /// <param name="sessionLock">Serializes the token operations of one session.</param>
    /// <param name="timeProvider">The clock.</param>
    /// <param name="logger">The logger; it never receives a token.</param>
    /// <exception cref="ArgumentNullException">Thrown when an argument is <see langword="null" />.</exception>
    public UserTokenManager(
        IDistributedCache cache,
        IDataProtectionProvider dataProtectionProvider,
        IHttpClientFactory httpClientFactory,
        IOptionsMonitor<OpenIdConnectOptions> openIdConnectOptions,
        IOptions<UserTokenManagementOptions> options,
        IUserSessionLock sessionLock,
        TimeProvider timeProvider,
        ILogger<UserTokenManager> logger)
        : this(cache, dataProtectionProvider, httpClientFactory, openIdConnectOptions, options, sessionLock, timeProvider, logger, null)
    {
    }

    /// <summary>Initializes token management with atomic conditional user-token mutations.</summary>
    /// <param name="cache">The distributed cache that holds the protected tokens.</param>
    /// <param name="dataProtectionProvider">Protects the tokens at rest.</param>
    /// <param name="httpClientFactory">Creates the identity provider client.</param>
    /// <param name="openIdConnectOptions">Supplies the client credentials and discovery document.</param>
    /// <param name="options">The token management options.</param>
    /// <param name="sessionLock">Serializes token operations of one session.</param>
    /// <param name="timeProvider">The clock.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="concurrency">Atomic mutation support for the same backing store, or null for a cache recheck under the session lock.</param>
    /// <exception cref="ArgumentNullException">Thrown when a required argument is null.</exception>
    public UserTokenManager(
        IDistributedCache cache,
        IDataProtectionProvider dataProtectionProvider,
        IHttpClientFactory httpClientFactory,
        IOptionsMonitor<OpenIdConnectOptions> openIdConnectOptions,
        IOptions<UserTokenManagementOptions> options,
        IUserSessionLock sessionLock,
        TimeProvider timeProvider,
        ILogger<UserTokenManager> logger,
        IUserTokenCacheConcurrency? concurrency)
    {
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(dataProtectionProvider);
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        ArgumentNullException.ThrowIfNull(openIdConnectOptions);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(sessionLock);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);

        _options = options.Value;
        _store = new ProtectedTokenCache(cache, dataProtectionProvider, _options.CacheKeyPrefix, concurrency);
        _identityProvider = new IdentityProviderClient(httpClientFactory, openIdConnectOptions, _options, logger);
        _sessionLock = sessionLock;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <summary>
    /// Starts a token session for a user who just signed in: adds fresh session claims to <paramref name="user" />, replacing
    /// any it carried, and stores the tokens.
    /// </summary>
    /// <param name="user">The principal that the cookie will carry; it is changed in place.</param>
    /// <param name="tokens">The tokens from the identity provider's token response.</param>
    /// <param name="ct">Cancels the store.</param>
    /// <returns>A task that completes when the tokens are stored.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="user" /> or <paramref name="tokens" /> is <see langword="null" />.</exception>
    /// <remarks>
    /// The OpenID Connect events registered by <see cref="UserTokenManagementServiceCollectionExtensions.AddUserTokenManagement" />
    /// call this after a validated login, once they have ended the session of the cookie the login replaces with <see cref="SignOut" />.
    /// </remarks>
    public async Task StartSession(ClaimsPrincipal user, UserTokenSet tokens, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(tokens);

        var session = new UserSession(
            WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32)),
            _timeProvider.GetUtcNow().Add(_options.SessionLifetime));
        UserSession.RemoveClaims(user);
        user.AddIdentity(UserSession.CreateIdentity(session.Id, session.ExpiresAt));
        await _store.SetUserTokens(session, tokens, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Returns the user's access token, refreshing it with the refresh_token grant when it is within
    /// <see cref="UserTokenManagementOptions.RefreshBeforeExpiry" /> of expiring.
    /// </summary>
    /// <param name="user">The signed-in user.</param>
    /// <param name="ct">Cancels the wait for a refresh another caller started; a started refresh is completed regardless.</param>
    /// <returns>The access token, or why none is available.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="user" /> is <see langword="null" />.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="ct" /> is canceled.</exception>
    /// <remarks>
    /// A refresh rejected with <c>invalid_grant</c>, or an expired token without a refresh token, ends the token session:
    /// <see cref="UserTokenFailure.RefreshFailed" />, and the cookie is rejected on the next request. An identity provider
    /// that fails, times out, answers unusably or refuses the client or request with another error keeps the tokens and
    /// yields the stored access token while it has not expired, otherwise <see cref="UserTokenFailure.IdentityProviderUnavailable" />.
    /// </remarks>
    public async Task<UserTokenResult> GetAccessToken(ClaimsPrincipal user, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(user);

        var (session, failure) = ActiveSession(user);
        if (session is null)
        {
            return UserTokenResult.Failed(failure);
        }

        var tokens = await _store.GetUserTokens(session, ct).ConfigureAwait(false);
        if (tokens is null)
        {
            return UserTokenResult.Failed(UserTokenFailure.NoTokens);
        }

        if (IsFresh(tokens.AccessTokenExpiresAt))
        {
            return UserTokenResult.Success(tokens.AccessToken, tokens.AccessTokenExpiresAt);
        }

        var held = await _sessionLock.Acquire(session.Id, ct).ConfigureAwait(false);
        await using (held.ConfigureAwait(false))
        {
            return await GetOrRefreshLocked(session).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Returns an access token for <paramref name="audience" />, exchanged from the user's access token (RFC 8693) and cached
    /// per session and audience until it is within <see cref="UserTokenManagementOptions.RefreshBeforeExpiry" /> of expiring.
    /// </summary>
    /// <param name="user">The signed-in user.</param>
    /// <param name="audience">The <c>audience</c> to request.</param>
    /// <param name="scope">The optional <c>scope</c> to request.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>The exchanged token, or why none is available.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="user" /> or <paramref name="audience" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="audience" /> is empty or white space, or <paramref name="scope" /> is empty or white space.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="ct" /> is canceled.</exception>
    /// <remarks>
    /// A cached token is reused only while the user's access token is the one it was exchanged from. Sign-out removes the
    /// cached tokens of the targets registered with <see cref="UserTokenHttpClientBuilderExtensions.AddTokenExchangeHandler" />;
    /// other targets expire with the session.
    /// </remarks>
    public async Task<UserTokenResult> GetAudienceAccessToken(ClaimsPrincipal user, string audience, string? scope, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentException.ThrowIfNullOrWhiteSpace(audience);
        if (scope is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(scope);
        }

        var (session, failure) = ActiveSession(user);
        if (session is null)
        {
            return UserTokenResult.Failed(failure);
        }

        var source = await GetAccessToken(user, ct).ConfigureAwait(false);
        if (!source.Succeeded)
        {
            return source;
        }

        var target = new TokenExchangeTarget(audience, scope);
        var cached = await _store.GetExchangedToken(session, target, source.AccessToken, ct).ConfigureAwait(false);
        if (cached is { } hit && IsFresh(hit.ExpiresAt))
        {
            return UserTokenResult.Success(hit.AccessToken, hit.ExpiresAt);
        }

        var held = await _sessionLock.Acquire(session.Id, ct).ConfigureAwait(false);
        await using (held.ConfigureAwait(false))
        {
            var current = await GetOrRefreshLocked(session).ConfigureAwait(false);
            if (!current.Succeeded)
            {
                return current;
            }

            cached = await _store.GetExchangedToken(session, target, current.AccessToken, ct).ConfigureAwait(false);
            if (cached is { } recheck && IsFresh(recheck.ExpiresAt))
            {
                return UserTokenResult.Success(recheck.AccessToken, recheck.ExpiresAt);
            }

            return await ExchangeLocked(session, target, current.AccessToken, ct).ConfigureAwait(false);
        }
    }

    /// <summary>Returns the stored tokens of the user's active session, as stored, without refreshing them.</summary>
    /// <param name="user">The signed-in user.</param>
    /// <param name="ct">Cancels the read.</param>
    /// <returns>The tokens, or <see langword="null" /> when the session is missing, expired or has no tokens.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="user" /> is <see langword="null" />.</exception>
    /// <remarks>Use it for the ID token, for example as <c>id_token_hint</c> at the end-session endpoint; call <see cref="GetAccessToken" /> for an access token.</remarks>
    public async Task<UserTokenSet?> GetTokens(ClaimsPrincipal user, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(user);

        var session = ActiveSession(user).Session;
        return session is null ? null : await _store.GetUserTokens(session, ct).ConfigureAwait(false);
    }

    /// <summary>Returns whether the user has a token session that has not expired and still holds tokens.</summary>
    /// <param name="user">The signed-in user.</param>
    /// <param name="ct">Cancels the read.</param>
    /// <returns><see langword="true" /> when the session is usable.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="user" /> is <see langword="null" />.</exception>
    public async Task<bool> HasActiveSession(ClaimsPrincipal user, CancellationToken ct)
    {
        return await GetTokens(user, ct).ConfigureAwait(false) is not null;
    }

    /// <summary>
    /// Ends the user's token session: removes its tokens and the cached exchanged tokens of the registered targets, then
    /// revokes the refresh token when <see cref="UserTokenManagementOptions.RevokeRefreshTokenOnSignOut" /> is set and the
    /// identity provider has a revocation endpoint.
    /// </summary>
    /// <param name="user">The user who signs out.</param>
    /// <param name="ct">Cancels the sign-out.</param>
    /// <returns>A task that completes when the tokens are removed and revocation was attempted.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="user" /> is <see langword="null" />.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="ct" /> is canceled.</exception>
    /// <remarks>A failed revocation is logged without the token and does not fail the sign-out; the tokens are removed either way.</remarks>
    public async Task SignOut(ClaimsPrincipal user, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(user);

        var session = UserSession.From(user);
        if (session is null)
        {
            return;
        }

        UserTokenSet? tokens;
        var held = await _sessionLock.Acquire(session.Id, ct).ConfigureAwait(false);
        await using (held.ConfigureAwait(false))
        {
            tokens = await _store.GetUserTokens(session, ct).ConfigureAwait(false);
            await _store.RemoveUserTokens(session, ct).ConfigureAwait(false);
            foreach (var target in _options.ExchangeTargets)
            {
                await _store.RemoveExchangedToken(session, target, ct).ConfigureAwait(false);
            }
        }

        if (_options.RevokeRefreshTokenOnSignOut && tokens?.RefreshToken is { } refreshToken
            && await _identityProvider.Revoke(refreshToken, ct).ConfigureAwait(false) is false)
        {
            LogRevocationFailed(_logger);
        }
    }

    private (UserSession? Session, UserTokenFailure Failure) ActiveSession(ClaimsPrincipal user)
    {
        var session = UserSession.From(user);
        if (session is null)
        {
            return (null, UserTokenFailure.NoSession);
        }

        return HasEnded(session) ? (null, UserTokenFailure.SessionExpired) : (session, UserTokenFailure.None);
    }

    private bool HasEnded(UserSession session)
    {
        return session.ExpiresAt <= _timeProvider.GetUtcNow();
    }

    private bool IsFresh(DateTimeOffset expiresAt)
    {
        return expiresAt - _options.RefreshBeforeExpiry > _timeProvider.GetUtcNow();
    }

    private DateTimeOffset ExpiresAfter(long seconds)
    {
        return _timeProvider.GetUtcNow().AddSeconds(Math.Min(seconds, MaximumLifetimeSeconds));
    }

    /// <summary>
    /// Re-reads the tokens under the session lock and refreshes them if they are still not fresh. A refresh that has started
    /// is completed even when the caller gives up, so a rotated refresh token is never lost.
    /// </summary>
    private async Task<UserTokenResult> GetOrRefreshLocked(UserSession session)
    {
        var entry = await _store.GetUserTokenVersion(session, CancellationToken.None).ConfigureAwait(false);
        if (entry is not { } current)
        {
            return UserTokenResult.Failed(UserTokenFailure.NoTokens);
        }

        var (tokens, version) = current;
        if (IsFresh(tokens.AccessTokenExpiresAt))
        {
            return UserTokenResult.Success(tokens.AccessToken, tokens.AccessTokenExpiresAt);
        }

        if (tokens.RefreshToken is null)
        {
            LogExpiredWithoutRefreshToken(_logger);
            return await RemoveRejectedTokens(session, version).ConfigureAwait(false);
        }

        var outcome = await _identityProvider.Refresh(tokens.RefreshToken, CancellationToken.None).ConfigureAwait(false);
        if (HasEnded(session))
        {
            _ = await _store.TryRemoveUserTokens(session, version, CancellationToken.None).ConfigureAwait(false);
            return UserTokenResult.Failed(UserTokenFailure.SessionExpired);
        }

        if (outcome.GrantRejected)
        {
            LogRefreshRejected(_logger);
            return await RemoveRejectedTokens(session, version).ConfigureAwait(false);
        }

        var answer = outcome.Answer;
        if (answer is null)
        {
            return await ReadCurrentAccessToken(session).ConfigureAwait(false);
        }

        if (string.IsNullOrWhiteSpace(answer.AccessToken) || answer.ExpiresIn is <= 0)
        {
            LogUnusableAnswer(_logger, IdentityProviderClient.RefreshOperation);
            if (!string.IsNullOrWhiteSpace(answer.RefreshToken))
            {
                var rotated = new UserTokenSet(
                    tokens.AccessToken,
                    tokens.AccessTokenExpiresAt,
                    answer.RefreshToken,
                    tokens.IdentityToken,
                    tokens.AccessTokenType,
                    tokens.Scope);
                if (await _store.TryReplaceUserTokens(session, version, rotated, CancellationToken.None).ConfigureAwait(false))
                {
                    return StillValidOrUnavailable(rotated);
                }
            }

            return await ReadCurrentAccessToken(session).ConfigureAwait(false);
        }

        if (answer.IdentityToken is not null && tokens.IdentityToken is not null
            && !IdentityTokenSubject.Matches(tokens.IdentityToken, answer.IdentityToken))
        {
            LogIdentityTokenMismatch(_logger);
            return await ReadCurrentAccessToken(session).ConfigureAwait(false);
        }

        var refreshed = new UserTokenSet(
            answer.AccessToken,
            answer.ExpiresIn is { } seconds ? ExpiresAfter(seconds) : _timeProvider.GetUtcNow().Add(_options.DefaultAccessTokenLifetime),
            answer.RefreshToken ?? tokens.RefreshToken,
            answer.IdentityToken ?? tokens.IdentityToken,
            answer.TokenType ?? tokens.AccessTokenType,
            answer.Scope ?? tokens.Scope);
        return await _store.TryReplaceUserTokens(session, version, refreshed, CancellationToken.None).ConfigureAwait(false)
            ? UserTokenResult.Success(refreshed.AccessToken, refreshed.AccessTokenExpiresAt)
            : await ReadCurrentAccessToken(session).ConfigureAwait(false);
    }

    private async Task<UserTokenResult> RemoveRejectedTokens(UserSession session, byte[] version)
    {
        return await _store.TryRemoveUserTokens(session, version, CancellationToken.None).ConfigureAwait(false)
            ? UserTokenResult.Failed(UserTokenFailure.RefreshFailed)
            : await ReadCurrentAccessToken(session).ConfigureAwait(false);
    }

    private async Task<UserTokenResult> ReadCurrentAccessToken(UserSession session)
    {
        if (HasEnded(session))
        {
            return UserTokenResult.Failed(UserTokenFailure.SessionExpired);
        }

        var tokens = await _store.GetUserTokens(session, CancellationToken.None).ConfigureAwait(false);
        return tokens is null ? UserTokenResult.Failed(UserTokenFailure.NoTokens) : StillValidOrUnavailable(tokens);
    }

    /// <summary>
    /// The result of a refresh that got no usable answer: the stored access token while it has not expired, since it was
    /// refreshed only because it was inside <see cref="UserTokenManagementOptions.RefreshBeforeExpiry" />.
    /// </summary>
    private UserTokenResult StillValidOrUnavailable(UserTokenSet tokens)
    {
        return tokens.AccessTokenExpiresAt > _timeProvider.GetUtcNow()
            ? UserTokenResult.Success(tokens.AccessToken, tokens.AccessTokenExpiresAt)
            : UserTokenResult.Failed(UserTokenFailure.IdentityProviderUnavailable);
    }

    private async Task<UserTokenResult> ExchangeLocked(
        UserSession session,
        TokenExchangeTarget target,
        string sourceAccessToken,
        CancellationToken ct)
    {
        var outcome = await _identityProvider.Exchange(sourceAccessToken, target, ct).ConfigureAwait(false);
        if (HasEnded(session))
        {
            return UserTokenResult.Failed(UserTokenFailure.SessionExpired);
        }

        if (outcome.Rejected)
        {
            LogExchangeRejected(_logger, target.Audience);
            return UserTokenResult.Failed(UserTokenFailure.ExchangeFailed);
        }

        var answer = outcome.Answer;
        if (answer is null)
        {
            return UserTokenResult.Failed(UserTokenFailure.IdentityProviderUnavailable);
        }

        if (string.IsNullOrWhiteSpace(answer.AccessToken)
            || string.Equals(answer.AccessToken, sourceAccessToken, StringComparison.Ordinal)
            || !string.Equals(answer.TokenType, BearerTokenType, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(answer.IssuedTokenType, AccessTokenTypeIdentifier, StringComparison.Ordinal)
            || answer.ExpiresIn is not > 0)
        {
            LogUnusableAnswer(_logger, IdentityProviderClient.ExchangeOperation);
            return UserTokenResult.Failed(UserTokenFailure.ExchangeFailed);
        }

        var expiresAt = ExpiresAfter(answer.ExpiresIn.Value);
        await _store.SetExchangedToken(session, target, sourceAccessToken, answer.AccessToken, expiresAt, ct).ConfigureAwait(false);
        return UserTokenResult.Success(answer.AccessToken, expiresAt);
    }

    [LoggerMessage(4301, LogLevel.Warning, "The identity provider rejected the refresh token; the token session ended.", EventName = "RefreshTokenRejected")]
    private static partial void LogRefreshRejected(ILogger logger);

    [LoggerMessage(4302, LogLevel.Information, "The access token expired and there is no refresh token; the token session ended.", EventName = "TokenSessionExpired")]
    private static partial void LogExpiredWithoutRefreshToken(ILogger logger);

    [LoggerMessage(4303, LogLevel.Warning, "The identity provider {Operation} answer has no usable access token.", EventName = "UnusableTokenAnswer")]
    private static partial void LogUnusableAnswer(ILogger logger, string operation);

    [LoggerMessage(4304, LogLevel.Warning, "The refreshed ID token names another issuer or subject than the login; the refresh answer was discarded.", EventName = "IdentityTokenMismatch")]
    private static partial void LogIdentityTokenMismatch(ILogger logger);

    [LoggerMessage(4305, LogLevel.Warning, "The identity provider rejected the token exchange for audience {Audience}.", EventName = "TokenExchangeRejected")]
    private static partial void LogExchangeRejected(ILogger logger, string audience);

    [LoggerMessage(4306, LogLevel.Warning, "The refresh token could not be revoked at sign-out; the stored tokens were removed.", EventName = "RefreshTokenRevocationFailed")]
    private static partial void LogRevocationFailed(ILogger logger);
}
