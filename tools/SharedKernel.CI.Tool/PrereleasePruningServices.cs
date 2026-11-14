namespace PandaBear.CI.Tool;

/// <summary>Supplies what <c>prune-prereleases</c> uses to reach the GitHub API and to read its token.</summary>
/// <param name="CreateHttpHandler">Creates the HTTP handler for one run; the run disposes it.</param>
/// <param name="GetEnvironmentVariable">Reads an environment variable, such as <c>GH_TOKEN</c>, or returns <see langword="null"/>.</param>
internal sealed record PrereleasePruningServices(Func<HttpMessageHandler> CreateHttpHandler, Func<string, string?> GetEnvironmentVariable)
{
    /// <summary>Gets the services that the tool uses: HTTPS without cookies or redirects, and the process environment.</summary>
    public static PrereleasePruningServices Default { get; } = new(CreateHandler, Environment.GetEnvironmentVariable);

    // A redirect is refused rather than followed, so the token never reaches another address.
    private static SocketsHttpHandler CreateHandler() => new() { AllowAutoRedirect = false, UseCookies = false };
}
