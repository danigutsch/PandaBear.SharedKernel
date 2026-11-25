using System.Net.Http.Headers;
using SharedKernel.GitHub;

namespace SharedKernel.Repositories.GitHub;

/// <summary>
/// Supplies the HTTP client that the GitHub operations of this package send their requests with. A connection either
/// borrows a client that the caller configured, or creates an authenticated client from a handler and a token the
/// first time an operation needs it, so an operation that fails validation never needs a token.
/// </summary>
public sealed class GitHubConnection : IDisposable
{
    private readonly HttpClient? _borrowedClient;
    private readonly HttpMessageHandler? _handler;
    private readonly ProductInfoHeaderValue? _userAgent;
    private readonly string? _token;
    private readonly string? _missingTokenMessage;
    private HttpClient? _ownedClient;
    private bool _disposed;

    /// <summary>Creates a connection that sends requests with <paramref name="client"/>, which the caller keeps and disposes.</summary>
    /// <param name="client">A client that already sends the headers and credentials that GitHub expects.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="client"/> is <see langword="null"/>.</exception>
    public GitHubConnection(HttpClient client)
    {
        ArgumentNullException.ThrowIfNull(client);
        _borrowedClient = client;
    }

    /// <summary>
    /// Creates a connection that, on first use, creates a client over <paramref name="handler"/> with the GitHub REST
    /// headers and <paramref name="token"/>, as <see cref="GitHubApiClient.Create"/> does. The connection owns the
    /// handler.
    /// </summary>
    /// <param name="handler">The handler that sends the requests, such as one that refuses redirects.</param>
    /// <param name="userAgent">The product that names the caller.</param>
    /// <param name="token">The token, or <see langword="null"/> or white-space when none is available.</param>
    /// <param name="missingTokenMessage">The message of the <see cref="InvalidOperationException"/> that an operation throws when it needs a client and no token is available.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="handler"/> or <paramref name="userAgent"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="missingTokenMessage"/> is <see langword="null"/>, empty, or white-space.</exception>
    public GitHubConnection(HttpMessageHandler handler, ProductInfoHeaderValue userAgent, string? token, string missingTokenMessage)
    {
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentNullException.ThrowIfNull(userAgent);
        ArgumentException.ThrowIfNullOrWhiteSpace(missingTokenMessage);
        _handler = handler;
        _userAgent = userAgent;
        _token = string.IsNullOrWhiteSpace(token) ? null : token;
        _missingTokenMessage = missingTokenMessage;
    }

    /// <summary>Gets the client, creating it on first use.</summary>
    /// <exception cref="ObjectDisposedException">The connection is disposed.</exception>
    /// <exception cref="InvalidOperationException">The connection creates its client and no token is available.</exception>
    internal HttpClient Client
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_borrowedClient is not null)
            {
                return _borrowedClient;
            }

            if (_ownedClient is not null)
            {
                return _ownedClient;
            }

            if (_token is null)
            {
                throw new InvalidOperationException(_missingTokenMessage);
            }

            _ownedClient = GitHubApiClient.Create(_handler!, _userAgent!, _token);
            return _ownedClient;
        }
    }

    /// <summary>Disposes the client and handler that the connection created or owns. A borrowed client is kept.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_ownedClient is not null)
        {
            _ownedClient.Dispose();
        }
        else
        {
            _handler?.Dispose();
        }
    }
}
