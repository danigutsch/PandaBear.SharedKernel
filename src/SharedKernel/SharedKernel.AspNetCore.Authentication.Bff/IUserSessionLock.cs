namespace SharedKernel.AspNetCore.Authentication.Bff;

/// <summary>
/// Serializes token refresh, token exchange and sign-out for one token session.
/// </summary>
/// <remarks>
/// The default implementation locks within one process, which suffices when a session's requests and Blazor Server circuit
/// stay on one instance. When one session's requests can reach several instances at once, register an implementation backed
/// by a distributed lock, such as a PostgreSQL advisory lock, so that a rotated refresh token is never redeemed twice.
/// </remarks>
public interface IUserSessionLock
{
    /// <summary>
    /// Waits until the caller holds the lock of the session.
    /// </summary>
    /// <param name="sessionId">The opaque session identifier.</param>
    /// <param name="ct">Cancels the wait.</param>
    /// <returns>A handle that releases the lock when disposed.</returns>
    ValueTask<IAsyncDisposable> Acquire(string sessionId, CancellationToken ct);
}
