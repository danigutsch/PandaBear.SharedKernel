using SharedKernel.IntegrationTesting;

namespace SharedKernel.Aspire.Hosting.Umami.Testing;

/// <summary>
/// Resolves the Umami website that an assembly's tests use: the one in an Umami that another process owns when
/// <see cref="UmamiTestWebsite.ConnectionStringVariable"/> is set, otherwise one in an Umami that this source starts on its first
/// demand. Disposing the source stops only an Umami that it started.
/// </summary>
internal sealed class UmamiTestWebsiteSource : IAsyncDisposable
{
    private readonly Func<string, string?> _readEnvironment;
    private readonly OnDemandResource<UmamiTestWebsiteConnection?> _borrowed;
    private readonly OnDemandResource<StartedUmamiTestWebsite> _owned;

    /// <summary>Creates a source that starts its own Umami with <paramref name="start"/>.</summary>
    /// <param name="start">
    /// Starts Umami and sets the website up. It receives a token that disposal cancels while the start is pending, and the first demand
    /// runs it synchronously on the demanding thread, so a testing builder that it creates finds the caller's DCP metadata.
    /// </param>
    /// <param name="readEnvironment">
    /// Reads an environment variable; the process environment when it is <see langword="null"/>. Tests supply their own so that they never
    /// change a process-wide variable.
    /// </param>
    internal UmamiTestWebsiteSource(Func<CancellationToken, Task<StartedUmamiTestWebsite>> start, Func<string, string?>? readEnvironment = null)
    {
        ArgumentNullException.ThrowIfNull(start);
        _readEnvironment = readEnvironment ?? Environment.GetEnvironmentVariable;
        _borrowed = new OnDemandResource<UmamiTestWebsiteConnection?>(_ => Task.FromResult(UmamiTestWebsite.Borrowed(_readEnvironment)));
        _owned = new OnDemandResource<StartedUmamiTestWebsite>(start);
    }

    /// <summary>Gets the website, starting an Umami first when no owner supplied one.</summary>
    /// <param name="ct">Abandons only this caller's wait; it never cancels the shared start.</param>
    /// <returns>The website. A malformed variable and a failed start are cached: the failure reaches every later caller without another wait.</returns>
    /// <exception cref="InvalidOperationException">The variable is set but is no website connection string, or holds <see cref="UmamiTestWebsite.Unavailable"/>.</exception>
    internal async Task<UmamiTestWebsiteConnection> Get(CancellationToken ct)
    {
        if (await _borrowed.Get(ct).ConfigureAwait(false) is { } borrowed)
        {
            return borrowed;
        }

        var owned = await _owned.Get(ct).ConfigureAwait(false);
        return owned.Website;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        try
        {
            await _borrowed.DisposeAsync().ConfigureAwait(false);
        }
        finally
        {
            await _owned.DisposeAsync().ConfigureAwait(false);
        }
    }
}
