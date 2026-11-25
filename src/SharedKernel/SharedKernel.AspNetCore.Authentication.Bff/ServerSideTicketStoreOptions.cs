namespace SharedKernel.AspNetCore.Authentication.Bff;

/// <summary>
/// Options for <see cref="ServerSideTicketStore" />.
/// </summary>
public sealed class ServerSideTicketStoreOptions
{
    /// <summary>
    /// Gets or sets the prefix of every ticket key. Defaults to <c>bff-ticket:</c>.
    /// </summary>
    /// <remarks>Give each application its own prefix when applications share a cache.</remarks>
    public string KeyPrefix { get; set; } = "bff-ticket:";

    /// <summary>
    /// Gets or sets the absolute lifetime of a cookie session, counted from the first time its ticket is stored. Sliding
    /// renewals never extend a session past it. Defaults to eight hours.
    /// </summary>
    public TimeSpan AbsoluteLifetime { get; set; } = TimeSpan.FromHours(8);

    /// <summary>
    /// Gets or sets an optional sliding expiration of the cache entry: an entry not read for this long is evicted. Defaults
    /// to none.
    /// </summary>
    public TimeSpan? SlidingExpiration { get; set; }
}
