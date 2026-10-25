namespace PandaBear.RepoConfig.Tool;

/// <summary>
/// Supplies the working directory and the external services that the command tree uses.
/// </summary>
/// <param name="WorkingDirectory">The directory that a relative or omitted <c>--root</c> resolves against.</param>
internal sealed record RepoConfigToolEnvironment(string WorkingDirectory)
{
    /// <summary>
    /// Gets the HTTP client for GitHub requests, or <see langword="null"/> to authenticate from the process environment.
    /// </summary>
    public HttpClient? HttpClient
    {
        get; init;
    }

    /// <summary>
    /// Gets the Git repository adapter for automation steps, or <see langword="null"/> to run Git.
    /// </summary>
    public RoadmapGitRepository? GitRepository
    {
        get; init;
    }

    /// <summary>
    /// Gets the host that <c>verify-podman-dns</c> inspects, or <see langword="null"/> for the current machine.
    /// </summary>
    public PodmanDnsHost? PodmanHost
    {
        get; init;
    }

    /// <summary>
    /// Gets the host that <c>verify-devcontainer</c> uses, or <see langword="null"/> for the current machine.
    /// </summary>
    public DevcontainerHost? DevcontainerHost
    {
        get; init;
    }
}
