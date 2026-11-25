namespace SharedKernel.Aspire.Hosting.Umami.Testing;

/// <summary>An Umami test website that an Aspire application of the test assembly started, together with the way to stop it.</summary>
/// <param name="website">The started website.</param>
/// <param name="stop">Stops Umami and its PostgreSQL server by disposing the application that owns their containers.</param>
internal sealed class StartedUmamiTestWebsite(UmamiTestWebsiteConnection website, Func<ValueTask> stop) : IAsyncDisposable
{
    /// <summary>Gets the started website.</summary>
    public UmamiTestWebsiteConnection Website { get; } = website;

    /// <inheritdoc />
    public ValueTask DisposeAsync() => stop();
}
