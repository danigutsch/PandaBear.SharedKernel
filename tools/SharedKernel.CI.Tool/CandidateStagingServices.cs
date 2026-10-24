namespace PandaBear.CI.Tool;

/// <summary>Supplies what <c>stage-candidate</c> uses to reach Git, the network, and the clock.</summary>
/// <param name="Git">Runs Git in a directory and returns its standard output.</param>
/// <param name="CreateHttpHandler">Creates the HTTP handler for one run; the run disposes it.</param>
/// <param name="TimeProvider">Times requests and retries, and ages staging directories.</param>
/// <param name="Limits">Bounds downloads.</param>
internal sealed record CandidateStagingServices(
    Func<string[], string, CancellationToken, Task<string>> Git,
    Func<HttpMessageHandler> CreateHttpHandler,
    TimeProvider TimeProvider,
    CandidateDownloadLimits Limits)
{
    /// <summary>Gets the services that the tool uses: Git from <c>PATH</c>, HTTPS without cookies or automatic redirects, and the system clock.</summary>
    public static CandidateStagingServices Default { get; } = new(ProcessRunner.RunGit, CreateHandler, TimeProvider.System, CandidateDownloadLimits.Default);

    // Redirects are followed by the download loop, which checks that every target is HTTPS.
    private static SocketsHttpHandler CreateHandler() => new() { AllowAutoRedirect = false, UseCookies = false };
}
