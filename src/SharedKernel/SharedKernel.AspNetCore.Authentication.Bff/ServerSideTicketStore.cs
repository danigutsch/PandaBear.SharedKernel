using System.Globalization;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace PandaBear.AspNetCore.Authentication.Bff;

/// <summary>
/// Keeps cookie authentication tickets in an <see cref="IDistributedCache" />, protected with Data Protection; the browser
/// cookie carries only an opaque, random key.
/// </summary>
/// <remarks>
/// <para>
/// Each entry is protected under a purpose bound to its own key, so an entry copied to another key cannot be read. An entry
/// that cannot be read, or whose ticket has expired, is removed and reads as missing, which signs the user out.
/// </para>
/// <para>
/// The cache entry expires with the ticket, and never later than <see cref="ServerSideTicketStoreOptions.AbsoluteLifetime" />
/// after the session's first store, even when the cookie handler renews the ticket on a sliding expiration.
/// </para>
/// </remarks>
public sealed partial class ServerSideTicketStore : ITicketStore
{
    private const string ProtectorPurpose = "PandaBear.AspNetCore.Authentication.Bff.ServerSideTicketStore.v1";
    private const string SessionStartedItem = ".sharedkernel.bff.session-started";

    private readonly IDistributedCache _cache;
    private readonly IDataProtector _protector;
    private readonly ServerSideTicketStoreOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ServerSideTicketStore> _logger;

    /// <summary>Initializes a new instance of the <see cref="ServerSideTicketStore" /> class.</summary>
    /// <param name="cache">The distributed cache that holds the tickets.</param>
    /// <param name="dataProtectionProvider">Protects the tickets at rest.</param>
    /// <param name="options">The store options.</param>
    /// <param name="timeProvider">The clock.</param>
    /// <param name="logger">The logger; it never receives a ticket or key.</param>
    /// <exception cref="ArgumentNullException">Thrown when an argument is <see langword="null" />.</exception>
    /// <exception cref="OptionsValidationException">Thrown when the options are invalid.</exception>
    public ServerSideTicketStore(
        IDistributedCache cache,
        IDataProtectionProvider dataProtectionProvider,
        IOptions<ServerSideTicketStoreOptions> options,
        TimeProvider timeProvider,
        ILogger<ServerSideTicketStore> logger)
    {
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(dataProtectionProvider);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);

        _options = options.Value;
        var failures = new List<string>();
        if (string.IsNullOrWhiteSpace(_options.KeyPrefix))
        {
            failures.Add("KeyPrefix must be set.");
        }

        if (_options.AbsoluteLifetime <= TimeSpan.Zero)
        {
            failures.Add("AbsoluteLifetime must be positive.");
        }

        if (_options.SlidingExpiration <= TimeSpan.Zero)
        {
            failures.Add("SlidingExpiration must be positive when set.");
        }

        if (failures.Count > 0)
        {
            throw new OptionsValidationException(nameof(ServerSideTicketStoreOptions), typeof(ServerSideTicketStoreOptions), failures);
        }

        _cache = cache;
        _protector = dataProtectionProvider.CreateProtector(ProtectorPurpose);
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<string> StoreAsync(AuthenticationTicket ticket)
    {
        return StoreAsync(ticket, CancellationToken.None);
    }

    /// <inheritdoc />
    public Task<string> StoreAsync(AuthenticationTicket ticket, HttpContext httpContext, CancellationToken cancellationToken)
    {
        return StoreAsync(ticket, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<string> StoreAsync(AuthenticationTicket ticket, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ticket);

        var key = string.Concat(_options.KeyPrefix, WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32)));
        ticket.Properties.Items[SessionStartedItem] = _timeProvider.GetUtcNow().ToString("O", CultureInfo.InvariantCulture);
        await RenewAsync(key, ticket, cancellationToken).ConfigureAwait(false);
        return key;
    }

    /// <inheritdoc />
    public Task RenewAsync(string key, AuthenticationTicket ticket)
    {
        return RenewAsync(key, ticket, CancellationToken.None);
    }

    /// <inheritdoc />
    public Task RenewAsync(string key, AuthenticationTicket ticket, HttpContext httpContext, CancellationToken cancellationToken)
    {
        return RenewAsync(key, ticket, cancellationToken);
    }

    /// <inheritdoc />
    public async Task RenewAsync(string key, AuthenticationTicket ticket, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(ticket);

        var expiresAt = GetExpiry(ticket);
        if (expiresAt <= _timeProvider.GetUtcNow())
        {
            await _cache.RemoveAsync(key, cancellationToken).ConfigureAwait(false);
            return;
        }

        var protectedTicket = _protector.CreateProtector(key).Protect(TicketSerializer.Default.Serialize(ticket));
        await _cache.SetAsync(
            key,
            protectedTicket,
            new DistributedCacheEntryOptions { AbsoluteExpiration = expiresAt, SlidingExpiration = _options.SlidingExpiration },
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<AuthenticationTicket?> RetrieveAsync(string key)
    {
        return RetrieveAsync(key, CancellationToken.None);
    }

    /// <inheritdoc />
    public Task<AuthenticationTicket?> RetrieveAsync(string key, HttpContext httpContext, CancellationToken cancellationToken)
    {
        return RetrieveAsync(key, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<AuthenticationTicket?> RetrieveAsync(string key, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var protectedTicket = await _cache.GetAsync(key, cancellationToken).ConfigureAwait(false);
        if (protectedTicket is null)
        {
            return null;
        }

        AuthenticationTicket? ticket;
        try
        {
            ticket = TicketSerializer.Default.Deserialize(_protector.CreateProtector(key).Unprotect(protectedTicket));
        }
        catch (Exception exception) when (exception is CryptographicException or FormatException or InvalidDataException
            or EndOfStreamException or ArgumentException)
        {
            LogUnreadableTicket(_logger, exception.GetType().FullName ?? exception.GetType().Name);
            ticket = null;
        }

        if (ticket is null || GetExpiry(ticket) <= _timeProvider.GetUtcNow())
        {
            await _cache.RemoveAsync(key, cancellationToken).ConfigureAwait(false);
            return null;
        }

        return ticket;
    }

    /// <inheritdoc />
    public Task RemoveAsync(string key)
    {
        return RemoveAsync(key, CancellationToken.None);
    }

    /// <inheritdoc />
    public Task RemoveAsync(string key, HttpContext httpContext, CancellationToken cancellationToken)
    {
        return RemoveAsync(key, cancellationToken);
    }

    /// <inheritdoc />
    public Task RemoveAsync(string key, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        return _cache.RemoveAsync(key, cancellationToken);
    }

    /// <summary>
    /// The earlier of the ticket's own expiry and the end of the absolute lifetime counted from the session's first store.
    /// </summary>
    private DateTimeOffset GetExpiry(AuthenticationTicket ticket)
    {
        var started = ticket.Properties.Items.TryGetValue(SessionStartedItem, out var value)
            && DateTimeOffset.TryParseExact(value, "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed)
            ? parsed
            : ticket.Properties.IssuedUtc ?? _timeProvider.GetUtcNow();
        var absoluteEnd = started.Add(_options.AbsoluteLifetime);
        return ticket.Properties.ExpiresUtc is { } expiresUtc && expiresUtc < absoluteEnd ? expiresUtc : absoluteEnd;
    }

    [LoggerMessage(4307, LogLevel.Warning, "A stored cookie ticket could not be read and was removed. Error type: {ErrorType}.", EventName = "UnreadableTicketRemoved")]
    private static partial void LogUnreadableTicket(ILogger logger, string errorType);
}
