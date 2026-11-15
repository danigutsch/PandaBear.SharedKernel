namespace PandaBear.CI.GitHub;

/// <summary>
/// Locates the public download address of a GitHub release asset,
/// <c>https://github.com/&lt;owner&gt;/&lt;name&gt;/releases/download/&lt;tag&gt;/&lt;asset&gt;</c>, without a request.
/// </summary>
public sealed class GitHubReleaseAssets : IReleaseAssets
{
    private readonly string _repository;

    /// <summary>Creates the locator for the releases of <paramref name="repository"/>.</summary>
    /// <param name="repository">The repository as <c>owner/name</c>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="repository"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="repository"/> is not <c>owner/name</c>.</exception>
    public GitHubReleaseAssets(string repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        GitHubRepositoryName.Parse(repository, nameof(repository));
        _repository = repository;
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException"><paramref name="tag"/> or <paramref name="asset"/> is <see langword="null"/>.</exception>
    public Task<Uri> Locate(string tag, string asset, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(tag);
        ArgumentNullException.ThrowIfNull(asset);
        return Task.FromResult(new Uri($"https://github.com/{_repository}/releases/download/{Uri.EscapeDataString(tag)}/{Uri.EscapeDataString(asset)}"));
    }
}
